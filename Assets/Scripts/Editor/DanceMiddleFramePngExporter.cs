using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DanceMiddleFramePngExporter
{
    const string DefaultAtundaSubfolder = "atunda";
    const double AvatarLoadTimeoutSeconds = 60.0;
    const int PoseSettleTicks = 2;

    static PendingAvatarExport pendingAvatarExport;

    static readonly (string a, string b)[] BonePairs =
    {
        ("neck", "nose"),
        ("nose", "left-eye"),
        ("nose", "right-eye"),
        ("left-eye", "left-ear"),
        ("right-eye", "right-ear"),
        ("neck", "left-shoulder"),
        ("neck", "right-shoulder"),
        ("left-shoulder", "right-shoulder"),
        ("left-shoulder", "left-elbow"),
        ("left-elbow", "left-wrist"),
        ("right-shoulder", "right-elbow"),
        ("right-elbow", "right-wrist"),
        ("left-hip", "right-hip"),
        ("left-hip", "left-knee"),
        ("left-knee", "left-ankle"),
        ("left-ankle", "left-heel"),
        ("left-ankle", "left-big-toe-tip"),
        ("right-hip", "right-knee"),
        ("right-knee", "right-ankle"),
        ("right-ankle", "right-heel"),
        ("right-ankle", "right-big-toe-tip"),
    };

    [MenuItem("UPose/Export middle dance frame PNG...")]
    public static void ExportMiddleFramePng()
    {
        string sourcePath = GetSelectedJsonPath();
        if (string.IsNullOrEmpty(sourcePath))
        {
            sourcePath = EditorUtility.OpenFilePanel("Choose npz-json dance file", Application.streamingAssetsPath, "json");
        }

        if (string.IsNullOrEmpty(sourcePath))
        {
            return;
        }

        try
        {
            string jsonText = ReadJsonText(sourcePath);
            NpzMotionClip clip = NpzJsonMotionParser.Parse(jsonText, true);
            if (clip?.frames == null || clip.frames.Length == 0)
            {
                EditorUtility.DisplayDialog("Dance PNG export", "The selected file does not contain any frames.", "OK");
                return;
            }

            int frameIndex = clip.frames.Length / 2;
            NpzMotionFrame frame = clip.frames[frameIndex];
            if (frame == null)
            {
                EditorUtility.DisplayDialog("Dance PNG export", "The middle frame was empty.", "OK");
                return;
            }

            string danceName = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(danceName))
            {
                danceName = "Dance";
            }

            string outputPath = GetOutputPath(sourcePath, danceName);
            Texture2D image = RenderFrame(frame, 1024, 1024);
            byte[] pngBytes = image.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(image);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Application.dataPath);
            File.WriteAllBytes(outputPath, pngBytes);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Dance PNG export",
                "Saved middle frame for '" + danceName + "' to:\n" + outputPath,
                "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Dance PNG export failed", ex.Message, "OK");
        }
    }

    [MenuItem("UPose/Export all dance frame PNGs...")]
    public static void ExportAllMiddleFramePngs()
    {
        string outputFolder = EditorUtility.OpenFolderPanel("Choose PNG output folder", Application.dataPath, "");
        if (string.IsNullOrEmpty(outputFolder))
        {
            return;
        }

        List<DanceSource> sources = CollectAllDanceSources();
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("Dance PNG export", "No dance JSON files were found.", "OK");
            return;
        }

        int exported = 0;
        int skipped = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < sources.Count; i++)
            {
                DanceSource source = sources[i];
                EditorUtility.DisplayProgressBar(
                    "Exporting dance PNGs",
                    source.danceName + " (" + (i + 1) + "/" + sources.Count + ")",
                    (i + 1f) / sources.Count);

                string outputPath = Path.Combine(outputFolder, SanitizeFileName(source.danceName) + ".png");
                if (ExportSingleDancePng(source.sourcePath, outputPath, out string reason))
                {
                    exported++;
                    continue;
                }

                if (string.IsNullOrEmpty(reason))
                {
                    failed++;
                    continue;
                }

                skipped++;
                Debug.LogWarning("Dance PNG export skipped '" + source.danceName + "': " + reason);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Dance PNG export",
            "Exported " + exported + " dances to:\n" + outputFolder +
            (skipped > 0 ? "\nSkipped " + skipped + " unsupported or empty files." : string.Empty) +
            (failed > 0 ? "\nFailed " + failed + " files." : string.Empty),
            "OK");
    }

    [MenuItem("UPose/Export middle dance avatar PNG...")]
    public static void ExportMiddleDanceAvatarPng()
    {
        string sourcePath = GetSelectedJsonPath();
        if (string.IsNullOrEmpty(sourcePath))
        {
            sourcePath = EditorUtility.OpenFilePanel("Choose npz-json dance file", Application.streamingAssetsPath, "json");
        }

        if (string.IsNullOrEmpty(sourcePath))
        {
            return;
        }

        try
        {
            string jsonText = ReadJsonText(sourcePath);
            NpzMotionClip clip = NpzJsonMotionParser.Parse(jsonText, true);
            if (clip?.frames == null || clip.frames.Length == 0)
            {
                EditorUtility.DisplayDialog("Dance avatar export", "The selected file does not contain any frames.", "OK");
                return;
            }

            int frameIndex = clip.frames.Length / 2;
            string danceName = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(danceName))
            {
                danceName = "Dance";
            }

            string outputPath = GetOutputPath(sourcePath, danceName);
            if (!TryStartAvatarExport(sourcePath, frameIndex, outputPath, danceName, out string reason))
            {
                EditorUtility.DisplayDialog("Dance avatar export failed", reason, "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Dance avatar export failed", ex.Message, "OK");
        }
    }

    [MenuItem("UPose/Export all dance avatar PNGs...")]
    public static void ExportAllDanceAvatarPngs()
    {
        string defaultFolder = Path.Combine(Application.dataPath, "UPoseExports", "DancePNGs");
        Directory.CreateDirectory(defaultFolder);
        string outputFolder = EditorUtility.OpenFolderPanel(
            "Choose avatar PNG output folder",
            defaultFolder,
            string.Empty);
        if (string.IsNullOrEmpty(outputFolder))
        {
            return;
        }

        List<DanceSource> sources = CollectAllDanceSources();
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("Dance avatar export", "No dance JSON files were found.", "OK");
            return;
        }

        if (!TryStartAvatarBatchExport(sources, outputFolder, out string reason))
        {
            EditorUtility.DisplayDialog("Dance avatar export failed", reason, "OK");
        }
    }

    static string GetSelectedJsonPath()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ToAbsolutePath(assetPath);
    }

    static List<DanceSource> CollectAllDanceSources()
    {
        HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<DanceSource> sources = new List<DanceSource>();

        TextAsset[] resourceDances = Resources.LoadAll<TextAsset>("Dances");
        for (int i = 0; i < resourceDances.Length; i++)
        {
            TextAsset file = resourceDances[i];
            if (file == null || string.IsNullOrWhiteSpace(file.name))
            {
                continue;
            }

            if (!seenNames.Add(file.name))
            {
                continue;
            }

            string outputKey = SanitizeFileName(file.name);
            if (!seenOutputs.Add(outputKey))
            {
                continue;
            }

            sources.Add(new DanceSource
            {
                danceName = file.name,
                sourcePath = AssetDatabase.GetAssetPath(file),
            });
        }

        List<string> streamingDances = AtundaDanceCatalog.GetDanceIds(DefaultAtundaSubfolder);
        for (int i = 0; i < streamingDances.Count; i++)
        {
            string danceId = streamingDances[i];
            if (string.IsNullOrWhiteSpace(danceId))
            {
                continue;
            }

            string danceName = Path.GetFileNameWithoutExtension(danceId);
            if (string.IsNullOrWhiteSpace(danceName) || !seenNames.Add(danceName))
            {
                continue;
            }

            string outputKey = SanitizeFileName(danceName);
            if (!seenOutputs.Add(outputKey))
            {
                continue;
            }

            sources.Add(new DanceSource
            {
                danceName = danceName,
                sourcePath = Path.Combine(Application.streamingAssetsPath, DefaultAtundaSubfolder, danceName + ".json"),
            });
        }

        sources.Sort((a, b) => string.Compare(a.danceName, b.danceName, StringComparison.OrdinalIgnoreCase));
        return sources;
    }

    static bool TryGetMiddleFrameIndex(string sourcePath, out int frameIndex, out string reason)
    {
        frameIndex = 0;
        reason = null;

        try
        {
            string jsonText = ReadJsonText(sourcePath);
            NpzMotionClip clip = NpzJsonMotionParser.Parse(jsonText, true);
            if (clip?.frames == null || clip.frames.Length == 0)
            {
                reason = "file does not contain any pose frames";
                return false;
            }

            frameIndex = clip.frames.Length / 2;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    static bool TryStartAvatarExport(string sourcePath, int frameIndex, string outputPath, string danceName, out string reason)
    {
        reason = null;

        if (pendingAvatarExport.active)
        {
            reason = "Another avatar export is already running. Wait for it to finish before starting a new one.";
            return false;
        }

        pendingAvatarExport = new PendingAvatarExport
        {
            sourcePath = sourcePath,
            frameIndex = frameIndex,
            outputPath = outputPath,
            danceName = danceName,
            startedAt = EditorApplication.timeSinceStartup,
            active = true,
            isBatch = false,
            phase = AvatarExportPhase.WaitingForAvatar,
        };

        BeginAvatarExportUpdates();
        EditorUtility.DisplayProgressBar("Dance avatar export", "Waiting for avatar to finish loading...", 0.05f);
        return true;
    }

    static bool TryStartAvatarBatchExport(List<DanceSource> sources, string outputFolder, out string reason)
    {
        reason = null;

        if (pendingAvatarExport.active)
        {
            reason = "Another avatar export is already running. Wait for it to finish before starting a new one.";
            return false;
        }

        pendingAvatarExport = new PendingAvatarExport
        {
            active = true,
            isBatch = true,
            batchSources = sources,
            batchOutputFolder = outputFolder,
            batchIndex = 0,
            startedAt = EditorApplication.timeSinceStartup,
            phase = AvatarExportPhase.WaitingForAvatar,
        };

        BeginAvatarExportUpdates();
        EditorUtility.DisplayProgressBar("Exporting avatar PNGs", "Waiting for avatar to finish loading...", 0.02f);
        return true;
    }

    static void BeginAvatarExportUpdates()
    {
        EditorApplication.update -= ProcessPendingAvatarExport;
        EditorApplication.update += ProcessPendingAvatarExport;
    }

    static void ProcessPendingAvatarExport()
    {
        if (!pendingAvatarExport.active)
        {
            EditorApplication.update -= ProcessPendingAvatarExport;
            EditorUtility.ClearProgressBar();
            return;
        }

        if (EditorApplication.timeSinceStartup - pendingAvatarExport.startedAt > AvatarLoadTimeoutSeconds)
        {
            FailPendingAvatarExport("Timed out waiting for the avatar export to finish.");
            return;
        }

        AvatarAnimationPlayer player = FindMainAvatarPlayer();
        ReadyPlayerAvatar avatar = FindMainReadyPlayerAvatar();
        if (player == null || avatar == null)
        {
            EditorUtility.DisplayProgressBar(
                pendingAvatarExport.isBatch ? "Exporting avatar PNGs" : "Dance avatar export",
                "Waiting for avatar objects to appear...",
                0.1f);
            return;
        }

        if (!avatar.isLoaded())
        {
            EditorUtility.DisplayProgressBar(
                pendingAvatarExport.isBatch ? "Exporting avatar PNGs" : "Dance avatar export",
                "Waiting for avatar mesh to finish loading...",
                0.2f);
            return;
        }

        if (pendingAvatarExport.isBatch)
        {
            ProcessPendingAvatarBatchExport(player, avatar);
            return;
        }

        ProcessPendingSingleAvatarExport(player, avatar);
    }

    static void ProcessPendingSingleAvatarExport(AvatarAnimationPlayer player, ReadyPlayerAvatar avatar)
    {
        switch (pendingAvatarExport.phase)
        {
            case AvatarExportPhase.WaitingForAvatar:
                pendingAvatarExport.phase = AvatarExportPhase.SettlingPose;
                pendingAvatarExport.settleTicksRemaining = PoseSettleTicks;
                if (!TryPrepareAvatarPose(
                        pendingAvatarExport.sourcePath,
                        pendingAvatarExport.frameIndex,
                        player,
                        avatar,
                        out string prepareReason))
                {
                    FailPendingAvatarExport(prepareReason ?? "Failed to prepare avatar pose.");
                }

                return;

            case AvatarExportPhase.SettlingPose:
                if (!AdvanceAvatarPoseSettle(player, avatar))
                {
                    return;
                }

                pendingAvatarExport.phase = AvatarExportPhase.Capturing;
                return;

            case AvatarExportPhase.Capturing:
                EditorUtility.DisplayProgressBar("Dance avatar export", "Rendering avatar PNG...", 0.85f);
                string expectedDanceId = Path.GetFileNameWithoutExtension(pendingAvatarExport.sourcePath);
                if (TryCaptureAvatarPng(
                        pendingAvatarExport.outputPath,
                        expectedDanceId,
                        player,
                        avatar,
                        out string reason))
                {
                    string message = "Saved avatar frame for '" + pendingAvatarExport.danceName + "' to:\n" + pendingAvatarExport.outputPath;
                    FinishPendingAvatarExport();
                    EditorUtility.DisplayDialog("Dance avatar export", message, "OK");
                    return;
                }

                FailPendingAvatarExport(reason ?? "Failed to render the avatar frame.");
                return;
        }
    }

    static void ProcessPendingAvatarBatchExport(AvatarAnimationPlayer player, ReadyPlayerAvatar avatar)
    {
        List<DanceSource> sources = pendingAvatarExport.batchSources;
        if (sources == null || sources.Count == 0)
        {
            CompletePendingAvatarBatchExport();
            return;
        }

        while (pendingAvatarExport.batchIndex < sources.Count)
        {
            DanceSource source = sources[pendingAvatarExport.batchIndex];
            float progress = (pendingAvatarExport.batchIndex + 1f) / sources.Count;
            EditorUtility.DisplayProgressBar(
                "Exporting avatar PNGs",
                source.danceName + " (" + (pendingAvatarExport.batchIndex + 1) + "/" + sources.Count + ")",
                progress);

            switch (pendingAvatarExport.phase)
            {
                case AvatarExportPhase.WaitingForAvatar:
                    if (!TryGetMiddleFrameIndex(source.sourcePath, out int frameIndex, out string frameReason))
                    {
                        RecordBatchExportFailure(source.danceName, frameReason, countAsSkipped: !string.IsNullOrEmpty(frameReason));
                        continue;
                    }

                    pendingAvatarExport.sourcePath = source.sourcePath;
                    pendingAvatarExport.frameIndex = frameIndex;
                    pendingAvatarExport.outputPath = Path.Combine(
                        pendingAvatarExport.batchOutputFolder,
                        SanitizeFileName(source.danceName) + ".png");
                    pendingAvatarExport.danceName = source.danceName;
                    pendingAvatarExport.phase = AvatarExportPhase.SettlingPose;
                    pendingAvatarExport.settleTicksRemaining = PoseSettleTicks;

                    if (!TryPrepareAvatarPose(source.sourcePath, frameIndex, player, avatar, out string prepareReason))
                    {
                        RecordBatchExportFailure(source.danceName, prepareReason, countAsSkipped: !string.IsNullOrEmpty(prepareReason));
                        continue;
                    }

                    return;

                case AvatarExportPhase.SettlingPose:
                    if (!AdvanceAvatarPoseSettle(player, avatar))
                    {
                        return;
                    }

                    pendingAvatarExport.phase = AvatarExportPhase.Capturing;
                    return;

                case AvatarExportPhase.Capturing:
                    string expectedDanceId = Path.GetFileNameWithoutExtension(source.sourcePath);
                    if (TryCaptureAvatarPng(pendingAvatarExport.outputPath, expectedDanceId, player, avatar, out string captureReason))
                    {
                        pendingAvatarExport.batchExported++;
                    }
                    else
                    {
                        RecordBatchExportFailure(source.danceName, captureReason, countAsSkipped: !string.IsNullOrEmpty(captureReason));
                    }

                    pendingAvatarExport.batchIndex++;
                    pendingAvatarExport.phase = AvatarExportPhase.WaitingForAvatar;
                    continue;
            }
        }

        CompletePendingAvatarBatchExport();
    }

    static void RecordBatchExportFailure(string danceName, string reason, bool countAsSkipped)
    {
        if (countAsSkipped)
        {
            pendingAvatarExport.batchSkipped++;
            Debug.LogWarning("Dance avatar export skipped '" + danceName + "': " + reason);
        }
        else
        {
            pendingAvatarExport.batchFailed++;
        }

        pendingAvatarExport.batchIndex++;
        pendingAvatarExport.phase = AvatarExportPhase.WaitingForAvatar;
    }

    static void CompletePendingAvatarBatchExport()
    {
        string outputFolder = pendingAvatarExport.batchOutputFolder;
        int exported = pendingAvatarExport.batchExported;
        int skipped = pendingAvatarExport.batchSkipped;
        int failed = pendingAvatarExport.batchFailed;

        FinishPendingAvatarExport();
        AssetDatabase.Refresh();
        DancePreviewImageCatalog.ClearCache();
        RefreshOpenDanceMenuPreviews();

        EditorUtility.DisplayDialog(
            "Dance avatar export",
            "Exported " + exported + " avatar PNGs to:\n" + outputFolder +
            (skipped > 0 ? "\nSkipped " + skipped + " unsupported or unavailable files." : string.Empty) +
            (failed > 0 ? "\nFailed " + failed + " files." : string.Empty),
            "OK");
    }

    static bool AdvanceAvatarPoseSettle(AvatarAnimationPlayer player, ReadyPlayerAvatar avatar)
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        ApplyAvatarPoseForExport(player, avatar);

        if (pendingAvatarExport.settleTicksRemaining > 0)
        {
            pendingAvatarExport.settleTicksRemaining--;
            return false;
        }

        return true;
    }

    static void FailPendingAvatarExport(string reason)
    {
        FinishPendingAvatarExport();
        EditorUtility.DisplayDialog("Dance avatar export failed", reason, "OK");
    }

    static void FinishPendingAvatarExport()
    {
        pendingAvatarExport = default;
        EditorApplication.update -= ProcessPendingAvatarExport;
        EditorUtility.ClearProgressBar();
    }

    static bool ExportSingleDancePng(string sourcePath, string outputPath, out string reason)
    {
        reason = null;

        try
        {
            string jsonText = ReadJsonText(sourcePath);
            NpzMotionClip clip = NpzJsonMotionParser.Parse(jsonText, true);
            if (clip?.frames == null || clip.frames.Length == 0)
            {
                reason = "file does not contain any pose frames";
                return false;
            }

            int frameIndex = clip.frames.Length / 2;
            NpzMotionFrame frame = clip.frames[frameIndex];
            if (frame == null)
            {
                reason = "middle frame was empty";
                return false;
            }

            Texture2D image = RenderFrame(frame, 1024, 1024);
            try
            {
                byte[] pngBytes = image.EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Application.dataPath);
                File.WriteAllBytes(outputPath, pngBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    static bool TryRenderAvatarFrame(string sourcePath, int frameIndex, string outputPath, out string reason)
    {
        AvatarAnimationPlayer player = FindMainAvatarPlayer();
        ReadyPlayerAvatar avatar = FindMainReadyPlayerAvatar();
        if (player == null)
        {
            reason = "No AvatarAnimationPlayer was found in the open scene. Open the scene that contains the avatar before exporting.";
            return false;
        }

        if (avatar == null)
        {
            reason = "No ReadyPlayerAvatar was found in the open scene.";
            return false;
        }

        if (!TryPrepareAvatarPose(sourcePath, frameIndex, player, avatar, out reason))
        {
            return false;
        }

        for (int i = 0; i < PoseSettleTicks; i++)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            ApplyAvatarPoseForExport(player, avatar);
        }

        string expectedDanceId = Path.GetFileNameWithoutExtension(sourcePath);
        return TryCaptureAvatarPng(outputPath, expectedDanceId, player, avatar, out reason);
    }

    static bool TryPrepareAvatarPose(
        string sourcePath,
        int frameIndex,
        AvatarAnimationPlayer player,
        ReadyPlayerAvatar avatar,
        out string reason)
    {
        reason = null;

        if (player == null)
        {
            reason = "No AvatarAnimationPlayer was found in the open scene. Open the scene that contains the avatar before exporting.";
            return false;
        }

        if (avatar == null)
        {
            reason = "No ReadyPlayerAvatar was found in the open scene.";
            return false;
        }

        string danceName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(danceName))
        {
            danceName = "Dance";
        }

        player.PlayDance(danceName + ".json");
        player.SeekToFrame(frameIndex);

        if (!player.isPlaying)
        {
            reason = "Avatar player could not load dance '" + danceName + "'.";
            return false;
        }

        if (!string.Equals(player.CurrentDanceId, danceName, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Avatar player is on '" + player.CurrentDanceId + "' instead of '" + danceName + "'.";
            return false;
        }

        ApplyAvatarPoseForExport(player, avatar);
        return true;
    }

    static void ApplyAvatarPoseForExport(AvatarAnimationPlayer player, ReadyPlayerAvatar avatar)
    {
        if (player?.SyncedStick != null)
        {
            player.SyncedStick.SyncPlayback(player.FrameAccumulator);
        }

        avatar.ApplyDancePlayback(player);
    }

    static bool TryCaptureAvatarPng(
        string outputPath,
        string expectedDanceId,
        AvatarAnimationPlayer player,
        ReadyPlayerAvatar avatar,
        out string reason)
    {
        reason = null;

        if (player == null || avatar == null)
        {
            reason = "Avatar objects disappeared before capture.";
            return false;
        }

        if (!player.isPlaying
            || !string.Equals(player.CurrentDanceId, expectedDanceId, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Avatar player is on '" + player.CurrentDanceId + "' instead of '" + expectedDanceId + "'.";
            return false;
        }

        if (player.UseStickFigurePose
            && player.SyncedStick != null
            && !player.SyncedStick.MatchesDanceId(expectedDanceId))
        {
            reason = "Stick figure is still on a different dance than '" + expectedDanceId + "'.";
            return false;
        }

        Renderer[] renderers = FindAvatarRenderers(avatar.transform);
        if (renderers == null || renderers.Length == 0)
        {
            reason = "No visible avatar renderers were found in the open scene. Make sure the avatar is loaded and visible before exporting.";
            return false;
        }

        List<RendererState> hiddenRenderers = HideNonAvatarRenderers(avatar.transform);
        List<HiddenObjectState> hiddenObjects = HideStickFigureObjects();
        Camera captureCamera = null;
        RenderTexture capture = null;

        try
        {
            Bounds bounds = ComputeRendererBounds(renderers);
            captureCamera = CreateCaptureCamera(bounds, avatar.transform);

            capture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            captureCamera.targetTexture = capture;
            captureCamera.Render();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = capture;

            Texture2D texture = new Texture2D(capture.width, capture.height, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0);
            texture.Apply(false, false);

            byte[] pngBytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Application.dataPath);
            File.WriteAllBytes(outputPath, pngBytes);

            RenderTexture.active = previousActive;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
        finally
        {
            RestoreHiddenObjects(hiddenObjects);
            RestoreRendererStates(hiddenRenderers);

            if (captureCamera != null)
            {
                if (captureCamera.targetTexture != null)
                {
                    captureCamera.targetTexture = null;
                }

                UnityEngine.Object.DestroyImmediate(captureCamera.gameObject);
            }

            RenderTexture.active = null;
            if (capture != null)
            {
                capture.Release();
                UnityEngine.Object.DestroyImmediate(capture);
            }
        }
    }

    static List<RendererState> HideNonAvatarRenderers(Transform avatarRoot)
    {
        List<RendererState> states = new List<RendererState>();
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (renderer.transform == avatarRoot || renderer.transform.IsChildOf(avatarRoot))
            {
                continue;
            }

            states.Add(new RendererState
            {
                renderer = renderer,
                enabled = renderer.enabled,
            });
            renderer.enabled = false;
        }

        return states;
    }

    static List<HiddenObjectState> HideStickFigureObjects()
    {
        List<HiddenObjectState> states = new List<HiddenObjectState>();
        NpzStickFigureVisualizer[] sticks = UnityEngine.Object.FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        for (int i = 0; i < sticks.Length; i++)
        {
            NpzStickFigureVisualizer stick = sticks[i];
            if (stick == null)
            {
                continue;
            }

            GameObject obj = stick.gameObject;
            states.Add(new HiddenObjectState
            {
                gameObject = obj,
                active = obj.activeSelf,
            });
            obj.SetActive(false);
        }

        return states;
    }

    static Renderer[] FindAvatarRenderers(Transform avatarRoot)
    {
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        List<Renderer> visible = new List<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!renderer.gameObject.scene.IsValid())
            {
                continue;
            }

            if (renderer.gameObject.layer == LayerMask.NameToLayer("UI_Avatar"))
            {
                continue;
            }

            if (IsUnderStickFigure(renderer.transform))
            {
                continue;
            }

            if (avatarRoot != null && (renderer.transform == avatarRoot || renderer.transform.IsChildOf(avatarRoot)))
            {
                visible.Add(renderer);
                continue;
            }

            if (visible.Count == 0)
            {
                visible.Add(renderer);
            }
        }

        return visible.ToArray();
    }

    static AvatarAnimationPlayer FindMainAvatarPlayer()
    {
        AvatarAnimationPlayer[] players = UnityEngine.Object.FindObjectsOfType<AvatarAnimationPlayer>();
        AvatarAnimationPlayer fallback = null;
        for (int i = 0; i < players.Length; i++)
        {
            AvatarAnimationPlayer player = players[i];
            if (player == null)
            {
                continue;
            }

            if (player.gameObject.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = player;
            }

            if (player.gameObject.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return player;
            }
        }

        return fallback;
    }

    static ReadyPlayerAvatar FindMainReadyPlayerAvatar()
    {
        ReadyPlayerAvatar[] avatars = UnityEngine.Object.FindObjectsOfType<ReadyPlayerAvatar>();
        ReadyPlayerAvatar fallback = null;
        for (int i = 0; i < avatars.Length; i++)
        {
            ReadyPlayerAvatar avatar = avatars[i];
            if (avatar == null)
            {
                continue;
            }

            if (avatar.gameObject.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = avatar;
            }

            if (avatar.gameObject.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return avatar;
            }
        }

        return fallback;
    }

    static bool IsUnderStickFigure(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        NpzStickFigureVisualizer[] sticks = UnityEngine.Object.FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        for (int i = 0; i < sticks.Length; i++)
        {
            NpzStickFigureVisualizer stick = sticks[i];
            if (stick != null && (transform == stick.transform || transform.IsChildOf(stick.transform)))
            {
                return true;
            }
        }

        PreviewDanceStickFigure[] previewSticks = UnityEngine.Object.FindObjectsByType<PreviewDanceStickFigure>(FindObjectsSortMode.None);
        for (int i = 0; i < previewSticks.Length; i++)
        {
            PreviewDanceStickFigure previewStick = previewSticks[i];
            if (previewStick != null && (transform == previewStick.transform || transform.IsChildOf(previewStick.transform)))
            {
                return true;
            }
        }

        return false;
    }

    static void RestoreRendererStates(List<RendererState> states)
    {
        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].renderer != null)
            {
                states[i].renderer.enabled = states[i].enabled;
            }
        }
    }

    static void RestoreHiddenObjects(List<HiddenObjectState> states)
    {
        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].gameObject != null)
            {
                states[i].gameObject.SetActive(states[i].active);
            }
        }
    }

    static Bounds ComputeRendererBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return bounds;
    }

    static Camera CreateCaptureCamera(Bounds bounds, Transform avatarRoot)
    {
        Camera sceneCamera = FindMainSceneCaptureCamera();
        if (sceneCamera != null)
        {
            return CreateCaptureCameraFromScene(sceneCamera);
        }

        return CreateFallbackCaptureCamera(bounds, avatarRoot);
    }

    static Camera FindMainSceneCaptureCamera()
    {
        Camera main = Camera.main;
        if (IsUsableSceneCaptureCamera(main))
        {
            return main;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (!IsUsableSceneCaptureCamera(camera))
            {
                continue;
            }

            int score = 0;
            if (camera.CompareTag("MainCamera"))
            {
                score += 300;
            }

            if (camera.gameObject.name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 200;
            }

            if (camera.enabled && camera.gameObject.activeInHierarchy)
            {
                score += 100;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = camera;
            }
        }

        return best;
    }

    static bool IsUsableSceneCaptureCamera(Camera camera)
    {
        if (camera == null || !camera.gameObject.scene.IsValid())
        {
            return false;
        }

        string cameraName = camera.gameObject.name;
        if (cameraName.IndexOf("AvatarCamera", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        if (cameraName.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    static Camera CreateCaptureCameraFromScene(Camera sceneCamera)
    {
        GameObject cameraObject = new GameObject("DanceAvatarCaptureCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(sceneCamera);
        camera.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = null;
        return camera;
    }

    static Camera CreateFallbackCaptureCamera(Bounds bounds, Transform avatarRoot)
    {
        GameObject cameraObject = new GameObject("DanceAvatarCaptureCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.orthographic = false;
        camera.fieldOfView = 28f;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        Vector3 avatarForward = avatarRoot != null ? avatarRoot.forward : Vector3.forward;
        Vector3 avatarUp = avatarRoot != null ? avatarRoot.up : Vector3.up;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        float distance = Mathf.Max(radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad), radius * 1.75f);
        Vector3 center = bounds.center + avatarUp * (bounds.extents.y * 0.1f);
        Vector3 position = center - avatarForward.normalized * distance + avatarUp * (bounds.extents.y * 0.15f);

        camera.transform.position = position;
        camera.transform.LookAt(center, avatarUp);
        camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
        camera.farClipPlane = distance + radius * 4f;

        return camera;
    }

    static string ReadJsonText(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            return File.ReadAllText(sourcePath);
        }

        string assetPath = ToProjectRelative(sourcePath);
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (asset != null)
        {
            return asset.text;
        }

        throw new FileNotFoundException("Could not read dance JSON: " + sourcePath);
    }

    static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Dance";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = fileName;
        for (int i = 0; i < invalidChars.Length; i++)
        {
            sanitized = sanitized.Replace(invalidChars[i].ToString(), "_");
        }

        return sanitized;
    }

    static string GetOutputPath(string sourcePath, string danceName)
    {
        string directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Application.dataPath;
        }

        return Path.Combine(directory, danceName + ".png");
    }

    public static void RefreshOpenDanceMenuPreviews()
    {
        AvatarUIItem[] items = UnityEngine.Object.FindObjectsByType<AvatarUIItem>(FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                items[i].RefreshPreviewImage();
            }
        }
    }

    static Texture2D RenderFrame(NpzMotionFrame frame, int width, int height)
    {
        Color32 background = new Color32(0, 0, 0, 0);
        Color32 lineColor = new Color32(255, 255, 255, 255);
        Color32 jointColor = new Color32(40, 220, 140, 255);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        Dictionary<string, Vector3> points = CollectPoints(frame);
        if (points.Count == 0)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        ComputeBounds(points, out Vector2 min, out Vector2 max);
        float rangeX = Mathf.Max(max.x - min.x, 0.001f);
        float rangeY = Mathf.Max(max.y - min.y, 0.001f);
        float scale = Mathf.Min((width * 0.72f) / rangeX, (height * 0.72f) / rangeY);
        Vector2 center = (min + max) * 0.5f;
        Vector2 targetCenter = new Vector2(width * 0.5f, height * 0.5f);

        for (int i = 0; i < BonePairs.Length; i++)
        {
            if (!points.TryGetValue(BonePairs[i].a, out Vector3 a) || !points.TryGetValue(BonePairs[i].b, out Vector3 b))
            {
                continue;
            }

            Vector2 pa = Project(a, center, targetCenter, scale, height);
            Vector2 pb = Project(b, center, targetCenter, scale, height);
            DrawLine(pixels, width, height, pa, pb, lineColor, 3);
        }

        foreach (KeyValuePair<string, Vector3> entry in points)
        {
            Vector2 p = Project(entry.Value, center, targetCenter, scale, height);
            DrawDisc(pixels, width, height, p, 5, jointColor);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    static Dictionary<string, Vector3> CollectPoints(NpzMotionFrame frame)
    {
        Dictionary<string, Vector3> points = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        if (frame?.keypoints == null)
        {
            return points;
        }

        for (int i = 0; i < frame.keypoints.Length; i++)
        {
            NpzKeypoint keypoint = frame.keypoints[i];
            if (!string.IsNullOrWhiteSpace(keypoint.name))
            {
                points[keypoint.name] = keypoint.position;
            }
        }

        return points;
    }

    static void ComputeBounds(Dictionary<string, Vector3> points, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (Vector3 point in points.Values)
        {
            if (point.x < min.x) min.x = point.x;
            if (point.y < min.y) min.y = point.y;
            if (point.x > max.x) max.x = point.x;
            if (point.y > max.y) max.y = point.y;
        }

        if (float.IsInfinity(min.x) || float.IsInfinity(min.y))
        {
            min = Vector2.zero;
            max = Vector2.one;
        }
    }

    static Vector2 Project(Vector3 point, Vector2 center, Vector2 targetCenter, float scale, int height)
    {
        Vector2 normalized = new Vector2(point.x - center.x, point.y - center.y) * scale;
        Vector2 pixel = targetCenter + normalized;
        pixel.y = height - pixel.y;
        return pixel;
    }

    static void DrawLine(Color32[] pixels, int width, int height, Vector2 a, Vector2 b, Color32 color, int thickness)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(a, b, t);
            DrawDisc(pixels, width, height, p, thickness, color);
        }
    }

    static void DrawDisc(Color32[] pixels, int width, int height, Vector2 center, int radius, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radius));
        int radiusSq = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    pixels[y * width + x] = color;
                }
            }
        }
    }

    static string ToProjectRelative(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace("\\", "/");
        string normalized = absolutePath.Replace("\\", "/");
        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        return normalized;
    }

    static string ToAbsolutePath(string assetPath)
    {
        string normalized = assetPath.Replace("\\", "/");
        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(Application.dataPath, normalized.Substring("Assets/".Length));
        }

        return normalized;
    }

    struct DanceSource
    {
        public string danceName;
        public string sourcePath;
    }

    struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
    }

    struct HiddenObjectState
    {
        public GameObject gameObject;
        public bool active;
    }

    struct PendingAvatarExport
    {
        public bool active;
        public bool isBatch;
        public string sourcePath;
        public int frameIndex;
        public string outputPath;
        public string danceName;
        public double startedAt;
        public AvatarExportPhase phase;
        public int settleTicksRemaining;
        public List<DanceSource> batchSources;
        public int batchIndex;
        public string batchOutputFolder;
        public int batchExported;
        public int batchSkipped;
        public int batchFailed;
    }

    enum AvatarExportPhase
    {
        WaitingForAvatar,
        SettlingPose,
        Capturing,
    }
}