using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FashionSenseWardrobeManager;

/// <summary>
/// Handles silently applying/restoring Fashion Sense outfits on the player
/// and drawing the farmer sprite inside a given portrait box.
/// All Fashion Sense interop is done via reflection so we don't need a hard reference.
/// </summary>
internal sealed class OutfitPreviewRenderer
{
    // State

    private readonly IMonitor _monitor;
    private readonly CosmeticShoeManager _cosmeticShoeManager;

    private Dictionary<string, string>? _snapshot;
    private string? _snapshotShoeColor;
    private readonly Dictionary<string, string> _renamePreviewAliases = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeOutfitName;

    public bool IsPreviewActive => _activeOutfitName is not null;
    public string? ActiveOutfitName => _activeOutfitName;

    // Constructor

    public OutfitPreviewRenderer(IMonitor monitor, CosmeticShoeManager cosmeticShoeManager)
    {
        _monitor = monitor;
        _cosmeticShoeManager = cosmeticShoeManager;
    }

    // Outfit apply / restore

    /// <summary>
    /// Silently apply an outfit for preview.
    /// Saves a snapshot of the current FS modData so it can be restored later.
    /// Returns false if the outfit could not be applied.
    /// </summary>
    public bool ApplyOutfitForPreview(string outfitName)
    {
        _snapshot = TakeSnapshot();

        if (!SetOutfitSilently(outfitName))
        {
            _monitor.Log($"Could not apply the outfit '{outfitName}' for preview.", LogLevel.Warn);
            _snapshot = null;
            return false;
        }

        _activeOutfitName = outfitName;
        return true;
    }

    /// <summary>Restore the original outfit and clear preview state.</summary>
    public void RestoreOriginal()
    {
        RestoreSnapshot();
        _activeOutfitName = null;
        _snapshot         = null;
        _snapshotShoeColor = null;
    }

    /// <summary>
    /// Commit the current preview as the "real" outfit.
    /// The preview outfit stays equipped; we just forget the snapshot.
    /// </summary>
    public void CommitPreview()
    {
        _snapshot         = null;
        _snapshotShoeColor = null;
        _activeOutfitName = null;
    }

    /// <summary>Equip a saved outfit as a real change, without creating preview state.</summary>
    public bool EquipOutfitImmediately(string outfitName)
    {
        try
        {
            bool equipped = SetOutfitSilently(outfitName);
            if (equipped)
            {
                _snapshot = null;
                _activeOutfitName = null;
            }

            return equipped;
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error equipping Fashion Sense outfit '{outfitName}': {ex}", LogLevel.Warn);
            return false;
        }
    }

    /// <summary>Return whether a saved outfit can currently be resolved by Fashion Sense.</summary>
    public bool OutfitExists(string outfitName)
    {
        try
        {
            object? outfitManager = GetOutfitManager();
            return outfitManager is not null && DoesOutfitExist(outfitManager, outfitName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Rename a saved Fashion Sense outfit.</summary>
    public bool RenameOutfit(string oldName, string newName)
    {
        string from = oldName.Trim();
        string to = newName.Trim();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;

        if (from.Equals(to, StringComparison.Ordinal))
            return true;

        try
        {
            object? outfitManager = GetOutfitManager();

            if (outfitManager is null)
                return false;

            if (!DoesOutfitExist(outfitManager, from))
                return false;

            // Fashion Sense's real manager method is:
            // RenameOutfit(Farmer who, string originalName, string currentName)
            // It serializes the outfit list back into player.modData.
            object? result = InvokeExactCompatibleMethod(
                outfitManager,
                "RenameOutfit",
                Game1.player,
                from,
                to
            );

            bool persisted = result is not null
                || DoesOutfitExist(outfitManager, to)
                || SavedOutfitCollectionContains(to);

            if (!persisted)
            {
                _monitor.Log($"Fashion Sense did not persist rename from '{from}' to '{to}'.", LogLevel.Warn);
                return false;
            }

            // Some Fashion Sense versions serialize the new name immediately, but keep
            // the old outfit object cached until the menu is reopened. Keep a temporary
            // alias so previewing the freshly-renamed outfit works right away.
            string cachedLookupName = from;

            if (_renamePreviewAliases.TryGetValue(from, out string? previousCachedName))
            {
                cachedLookupName = previousCachedName;
                _renamePreviewAliases.Remove(from);
            }

            _renamePreviewAliases[to] = cachedLookupName;

            MarkSpriteDirty();
            return true;
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error renaming Fashion Sense outfit '{from}' to '{to}': {ex}", LogLevel.Warn);
            return false;
        }
    }

    /// <summary>Save the farmer's current Fashion Sense outfit under the given name.</summary>
    public bool SaveCurrentOutfit(string outfitName)
    {
        string name = outfitName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            object? outfitManager = GetOutfitManager();

            if (outfitManager is null)
                return false;

            // Fashion Sense's real manager method is:
            // CreateOutfit(Farmer who, string name)
            // It creates an Outfit from the player's current FS appearance and serializes it.
            object? created = InvokeExactCompatibleMethod(
                outfitManager,
                "CreateOutfit",
                Game1.player,
                name
            );

            if (created is null && !DoesOutfitExist(outfitManager, name))
            {
                _monitor.Log($"Fashion Sense did not persist saved outfit '{name}'.", LogLevel.Warn);
                return false;
            }

            MarkSpriteDirty();
            return DoesOutfitExist(outfitManager, name);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error saving current Fashion Sense outfit '{name}': {ex}", LogLevel.Warn);
            return false;
        }
    }

    /// <summary>Replace a saved Fashion Sense outfit with the farmer's current appearance.</summary>
    public bool OverrideOutfitWithCurrentAppearance(string outfitName)
    {
        string name = outfitName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            object? outfitManager = GetOutfitManager();
            if (outfitManager is null || !DoesOutfitExist(outfitManager, name))
                return false;

            if (!TryInvokeExactCompatibleMethod(
                    outfitManager,
                    "OverrideOutfit",
                    out _,
                    Game1.player,
                    name))
            {
                _monitor.Log("The installed Fashion Sense version does not expose OverrideOutfit.", LogLevel.Warn);
                return false;
            }

            MarkSpriteDirty();
            return true;
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error updating Fashion Sense outfit '{name}': {ex}", LogLevel.Warn);
            return false;
        }
    }

    /// <summary>Delete a saved Fashion Sense outfit.</summary>
    public bool DeleteOutfit(string outfitName)
    {
        string name = outfitName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            object? outfitManager = GetOutfitManager();

            if (outfitManager is null)
                return false;

            if (!DoesOutfitExist(outfitManager, name))
                return false;

            // Fashion Sense's real manager method is:
            // DeleteOutfit(Farmer who, string name)
            // It removes the outfit and serializes the list back into player.modData.
            InvokeExactCompatibleMethod(
                outfitManager,
                "DeleteOutfit",
                Game1.player,
                name
            );

            MarkSpriteDirty();
            return !DoesOutfitExist(outfitManager, name);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error deleting Fashion Sense outfit '{name}': {ex}", LogLevel.Warn);
            return false;
        }
    }

    // Drawing

    /// <summary>Scale multiplier applied on top of the normal 4× UI scale.</summary>
    private const float DrawScale = 1.5f;

    /// <summary>
    /// Offscreen canvas size (un-scaled pixels).
    /// Large enough for wings, umbrellas and other tall/wide accessories.
    /// </summary>
    private const int NativeW = 240;
    private const int NativeH = 300;

    // Cached render target — recreated if the device is lost
    private RenderTarget2D? _renderTarget;

    /// <summary>
    /// Draw the quick Ctrl+Click preview using the original compact positioning.
    /// This is intentionally separate from the expanded-menu renderer, so changing
    /// the quick preview size does not affect the large expanded preview panel.
    /// </summary>
    public void DrawFarmerQuickPreview(SpriteBatch b, Rectangle portraitFrameBounds, int facingDirection)
    {
        Farmer who = Game1.player;

        int savedFacing = who.FacingDirection;
        int savedFrame  = who.FarmerSprite.currentFrame;

        try
        {
            who.faceDirection(facingDirection);
            NormalizePreviewPose(who);

            Rectangle sourceRect = who.FarmerSprite.SourceRect;
            Vector2 position = GetQuickPreviewPosition(portraitFrameBounds, sourceRect);

            FarmerRenderer.isDrawingForUI = true;

            try
            {
                who.FarmerRenderer.draw(
                    b,
                    who.FarmerSprite.CurrentAnimationFrame,
                    who.FarmerSprite.CurrentFrame,
                    who.FarmerSprite.SourceRect,
                    position,
                    Vector2.Zero,
                    1f,
                    Color.White,
                    0f,
                    1f,
                    who
                );
            }
            finally
            {
                FarmerRenderer.isDrawingForUI = false;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error drawing quick farmer preview: {ex}", LogLevel.Trace);
        }
        finally
        {
            who.faceDirection(savedFacing);
            who.FarmerSprite.setCurrentSingleFrame(savedFrame);
        }
    }

    private static Vector2 GetQuickPreviewPosition(Rectangle portraitFrameBounds, Rectangle sourceRect)
    {
        const float uiScale = 4f;
        const float drawScale = 1f;
        const float offsetX = 0f;
        const float offsetY = -8f;   // small downward nudge so the sprite doesn't float above the grass
        const float bottomRoom = 18f;

        Rectangle bounds = new(
            portraitFrameBounds.X + 8,
            portraitFrameBounds.Y + 8,
            portraitFrameBounds.Width - 16,
            portraitFrameBounds.Height - 16
        );

        float realDrawWidth  = sourceRect.Width  * uiScale * drawScale;
        float realDrawHeight = sourceRect.Height * uiScale * drawScale;

        return new Vector2(
            bounds.Center.X - realDrawWidth / 2f + offsetX,
            bounds.Bottom - realDrawHeight - bottomRoom + offsetY
        );
    }

    /// <summary>
    /// Draw the farmer sprite centred on <paramref name="portraitBounds"/>.
    /// Large accessories (wings, umbrellas…) are intentionally allowed to overflow
    /// the portrait frame so they are never clipped.
    /// </summary>
    public void DrawFarmer(SpriteBatch b, Rectangle portraitBounds, int facingDirection, float yOffset = 0f)
    {
        Farmer who = Game1.player;

        int savedFacing = who.FacingDirection;
        int savedFrame  = who.FarmerSprite.currentFrame;

        try
        {
            who.faceDirection(facingDirection);
            NormalizePreviewPose(who);

            // Step 1: ensure our render target exists
            GraphicsDevice gd = Game1.graphics.GraphicsDevice;

            if (_renderTarget == null
                || _renderTarget.IsDisposed
                || _renderTarget.Width  != NativeW
                || _renderTarget.Height != NativeH)
            {
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget2D(gd, NativeW, NativeH,
                    false, SurfaceFormat.Color, DepthFormat.None);
            }

            // Step 2: render farmer into the large offscreen canvas
            RenderTarget2D? prevTarget = gd.GetRenderTargets().FirstOrDefault().RenderTarget as RenderTarget2D;

            gd.SetRenderTarget(_renderTarget);
            gd.Clear(Color.Transparent);

            using (var innerBatch = new SpriteBatch(gd))
            {
                innerBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend);

                FarmerRenderer.isDrawingForUI = true;

                try
                {
                    // Body sits at the bottom-centre of the canvas.
                    // Accessories extend upward into the generous empty space above.
                    float bodyW = who.FarmerSprite.SourceRect.Width  * 4f;
                    float bodyH = who.FarmerSprite.SourceRect.Height * 4f;

                    Vector2 pos = new(
                        NativeW / 2f - bodyW / 2f,
                        NativeH      - bodyH - 12f
                    );

                    who.FarmerRenderer.draw(
                        innerBatch,
                        who.FarmerSprite.CurrentAnimationFrame,
                        who.FarmerSprite.CurrentFrame,
                        who.FarmerSprite.SourceRect,
                        pos,
                        Vector2.Zero,
                        1f,
                        Color.White,
                        0f,
                        1f,
                        who
                    );
                }
                finally
                {
                    FarmerRenderer.isDrawingForUI = false;
                }

                innerBatch.End();
            }

            // Step 3: restore the previous render target
            gd.SetRenderTarget(prevTarget);

            // Step 4: blit scaled canvas, centred on the portrait box
            // The dest rect is intentionally bigger than the portrait box frame
            // so tall/wide accessories can overflow it naturally.
            float destW = NativeW * DrawScale;
            float destH = NativeH * DrawScale;

            Rectangle dest = new(
                (int)(portraitBounds.Center.X - destW / 2f),
                (int)(portraitBounds.Bottom   - destH + yOffset),
                (int)destW,
                (int)destH
            );

            b.Draw(_renderTarget, dest, Color.White);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Error drawing farmer preview: {ex}", LogLevel.Trace);
        }
        finally
        {
            who.faceDirection(savedFacing);
            who.FarmerSprite.setCurrentSingleFrame(savedFrame);
        }
    }

    // Private helpers – rendering

    private static void NormalizePreviewPose(Farmer who)
    {
        who.completelyStopAnimatingOrDoingAction();
        who.FarmerSprite.StopAnimation();
        who.faceDirection(who.FacingDirection);
    }

    // Private helpers – Fashion Sense reflection

    private static object? GetOutfitManager()
    {
        Type? fsType = FindType("FashionSense.FashionSense");
        return fsType is null ? null : GetStaticField(fsType, "outfitManager");
    }

    private static bool DoesOutfitExist(object outfitManager, string outfitName)
    {
        object? result = InvokeExactCompatibleMethod(
            outfitManager,
            "DoesOutfitExist",
            Game1.player,
            outfitName,
            false
        );

        if (result is bool existsWithPresetArg)
            return existsWithPresetArg;

        result = InvokeExactCompatibleMethod(
            outfitManager,
            "DoesOutfitExist",
            Game1.player,
            outfitName
        );

        if (result is bool exists)
            return exists;

        return GetOutfit(outfitManager, outfitName) is not null;
    }

    private static object? GetOutfit(object outfitManager, string outfitName)
    {
        return InvokeExactCompatibleMethod(
            outfitManager,
            "GetOutfit",
            Game1.player,
            outfitName,
            false
        ) ?? InvokeExactCompatibleMethod(
            outfitManager,
            "GetOutfit",
            Game1.player,
            outfitName
        );
    }

    private bool SetOutfitSilently(string outfitName)
    {
        object? outfitManager = GetOutfitManager();

        if (outfitManager is null)
            return false;

        object? outfit = GetOutfit(outfitManager, outfitName);

        if (outfit is null
            && _renamePreviewAliases.TryGetValue(outfitName, out string? cachedName))
        {
            outfit = GetOutfit(outfitManager, cachedName);
        }

        if (outfit is null)
            return false;

        InvokeExactCompatibleMethod(outfitManager, "SetOutfit", Game1.player, outfit);
        _cosmeticShoeManager.ApplyForOutfit(outfitName);
        MarkSpriteDirty();

        return true;
    }

    private Dictionary<string, string> TakeSnapshot()
    {
        var snapshot = new Dictionary<string, string>();
        _snapshotShoeColor = Game1.player.shoes.Value;

        foreach (string key in Game1.player.modData.Keys)
        {
            // Only snapshot the currently equipped Fashion Sense appearance.
            // Do NOT snapshot the saved outfit database; otherwise preview restore
            // can undo real changes like rename/delete/save made while the expanded
            // window is open.
            if (ShouldSnapshotFashionSenseKey(key))
                snapshot[key] = Game1.player.modData[key];
        }

        return snapshot;
    }

    private void RestoreSnapshot()
    {
        if (_snapshot is null)
            return;

        var toRemove = new List<string>();

        foreach (string key in Game1.player.modData.Keys)
        {
            // Remove only live appearance keys affected by previewing.
            // Never remove saved outfit storage keys, or rename/delete operations
            // will appear to work and then come back after reopening the menu.
            if (ShouldSnapshotFashionSenseKey(key))
                toRemove.Add(key);
        }

        foreach (string key in toRemove)
            Game1.player.modData.Remove(key);

        foreach (var (key, value) in _snapshot)
            Game1.player.modData[key] = value;

        if (_snapshotShoeColor is not null)
            Game1.player.changeShoeColor(_snapshotShoeColor);

        MarkSpriteDirty();
    }

    private void MarkSpriteDirty()
    {
        try
        {
            Type? fsType = FindType("FashionSense.FashionSense");

            MethodInfo? method = fsType?.GetMethod(
                "SetSpriteDirty",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method is null)
                return;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length == 2)
                method.Invoke(null, new object?[] { Game1.player, false });
            else if (parameters.Length == 1)
                method.Invoke(null, new object?[] { Game1.player });
        }
        catch { /* best-effort */ }
    }

    private static bool IsFsKey(string key)
        => key.StartsWith("FashionSense.", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldSnapshotFashionSenseKey(string key)
    {
        if (!IsFsKey(key))
            return false;

        // Saved outfits live here. This key must not be restored from a preview
        // snapshot because the expanded menu can rename, delete, and create outfits
        // while a preview is active.
        if (key.Equals("FashionSense.Outfit.Collection", StringComparison.OrdinalIgnoreCase))
            return false;

        // Mannequin outfit data is also persisted outfit data, not the player's
        // temporary preview appearance.
        if (key.Equals("FashionSense.Outfit.Mannequin.Data", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool SavedOutfitCollectionContains(string outfitName)
    {
        if (!Game1.player.modData.TryGetValue("FashionSense.Outfit.Collection", out string raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw.IndexOf(outfitName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? t = assembly.GetType(fullName);

            if (t is not null)
                return t;
        }

        return null;
    }

    private static object? GetStaticField(Type type, string name)
        => type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
               ?.GetValue(null);

    private static object? InvokeExactCompatibleMethod(object target, string name, params object?[] args)
        => TryInvokeExactCompatibleMethod(target, name, out object? result, args) ? result : null;

    private static bool TryInvokeExactCompatibleMethod(
        object target,
        string name,
        out object? result,
        params object?[] args)
    {
        result = null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (MethodInfo method in target.GetType().GetMethods(flags))
        {
            if (method.Name != name)
                continue;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != args.Length)
                continue;

            bool compatible = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                object? value = args[i];

                if (value is null)
                {
                    if (parameters[i].ParameterType.IsValueType
                        && Nullable.GetUnderlyingType(parameters[i].ParameterType) is null)
                    {
                        compatible = false;
                        break;
                    }

                    continue;
                }

                if (!parameters[i].ParameterType.IsAssignableFrom(value.GetType()))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible)
                continue;

            result = method.Invoke(target, args);
            return true;
        }

        return false;
    }

    private static object? InvokeMethod(object target, string name, params object?[] args)
    {
        MethodInfo? method = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);

        return method?.Invoke(target, args);
    }

    private static bool TryRemoveOutfitFromManagerCollections(object outfitManager, string outfitName, object? outfit)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in outfitManager.GetType().GetFields(flags))
        {
            if (TryRemoveFromCollection(field.GetValue(outfitManager), outfitName, outfit))
                return true;
        }

        foreach (PropertyInfo property in outfitManager.GetType().GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;

            try
            {
                if (TryRemoveFromCollection(property.GetValue(outfitManager), outfitName, outfit))
                    return true;
            }
            catch
            {
                // Ignore properties that throw.
            }
        }

        return false;
    }

    private static bool TryRemoveFromCollection(object? collection, string outfitName, object? outfit)
    {
        if (collection is null)
            return false;

        Type type = collection.GetType();

        // Dictionary-like collections keyed by outfit name.
        MethodInfo? containsKey = type.GetMethod("ContainsKey", new[] { typeof(string) });
        MethodInfo? removeString = type.GetMethod("Remove", new[] { typeof(string) });
        if (containsKey is not null && removeString is not null)
        {
            try
            {
                if (containsKey.Invoke(collection, new object?[] { outfitName }) is bool hasKey && hasKey)
                    return removeString.Invoke(collection, new object?[] { outfitName }) is not bool removed || removed;
            }
            catch
            {
                // Fall through to list-like handling.
            }
        }

        // List-like collections that support Remove(object).
        if (outfit is not null)
        {
            MethodInfo? removeObject = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Remove" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(outfit.GetType()));

            if (removeObject is not null)
            {
                try
                {
                    return removeObject.Invoke(collection, new object?[] { outfit }) is not bool removed || removed;
                }
                catch
                {
                    // Best-effort fallback only.
                }
            }
        }

        return false;
    }

    private static bool TrySetMember(object target, string memberName, object? value)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo? property = target.GetType().GetProperty(memberName, flags);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value);
            return true;
        }

        FieldInfo? field = target.GetType().GetField(memberName, flags);
        if (field is not null)
        {
            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static bool TryInvokeCompatibleMethod(
        object target,
        IEnumerable<string> methodNames,
        IEnumerable<object?[]> argumentSets,
        out object? result)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods = target.GetType().GetMethods(flags);

        foreach (string methodName in methodNames)
        {
            foreach (object?[] args in argumentSets)
            {
                foreach (MethodInfo method in methods)
                {
                    if (method.Name != methodName)
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length != args.Length)
                        continue;

                    bool compatible = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (!IsParameterCompatible(parameters[i].ParameterType, args[i]))
                        {
                            compatible = false;
                            break;
                        }
                    }

                    if (!compatible)
                        continue;

                    result = method.Invoke(target, args);
                    return true;
                }
            }
        }

        result = null;
        return false;
    }

    private static bool IsParameterCompatible(Type parameterType, object? value)
    {
        if (value is null)
            return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null;

        Type valueType = value.GetType();
        return parameterType.IsAssignableFrom(valueType);
    }
}
