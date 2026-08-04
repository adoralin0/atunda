using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;

public class JsonAnimationPlayer : MonoBehaviour
{
    [Header("Legacy API")]
    public float fps = 30f;
    public bool isPlaying = false;

    [Header("Input")]
    public TextAsset animationJson;
    public bool loadFromResources = true;
    public string resourcesFolder = "Dances";
    public string resourceFileName = "";
    public bool loadFromStreamingAssets = false;
    public string streamingAssetsFileName = "";
    public string externalFilePath = ""; // absolute path if you want to load directly from disk

    [Header("Rig")]
    public Transform avatarRoot;
    public bool autoDetectAvatarRoot = true;
    public string autoDetectAvatarRootName = "Armature";
    public Transform rootMotionTarget;
    public BoneBinding[] boneBindings;

    [Header("Playback")]
    public bool playOnStart;
    public bool loop = true;
    public float playbackSpeed = 1f;
    public bool applyRootMotion = true;
    public bool applyBonePositions = true;
    public bool applyBoneRotations = true;
    public bool useSourceQuaternions = true;
    public bool forceDisableBonePositions = false;
    public Vector3 defaultUp = Vector3.up;
    public Vector3 globalRotationOffsetEuler = new Vector3(0f, 180f, 0f);
    public bool usePoint3D = true;
    public float uniformScale = 1f;
    public Vector3 globalPositionOffset = Vector3.zero;

    private AvatarMotionExport motionData;
    private float[][] frames = Array.Empty<float[]>();
    private float startTime;
    private float[] currentRotations = Array.Empty<float>();
    private readonly Dictionary<string, BoneBinding> resolvedBindings = new Dictionary<string, BoneBinding>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector3> bindingSourceRestPositions = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Vector3> bindingTargetRestLocalPositions = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
    private float frameAccumulator;
    private int currentFrameIndex;
    private float nextAvatarRootScanTime;

    private void Start()
    {
        Debug.Log("JsonAnimationPlayer.Start: playOnStart=" + playOnStart + ", loadFromResources=" + loadFromResources + ", loadFromStreamingAssets=" + loadFromStreamingAssets + ", externalFilePath='" + externalFilePath + "'");
        if (playOnStart)
        {
            if (!string.IsNullOrEmpty(externalFilePath))
            {
                Debug.Log("JsonAnimationPlayer: Loading from externalFilePath: " + externalFilePath);
                PlayFromFile(externalFilePath);
            }
            else if (loadFromStreamingAssets)
            {
                Debug.Log("JsonAnimationPlayer: Loading from StreamingAssets: " + streamingAssetsFileName);
                PlayFromStreamingAssets(streamingAssetsFileName);
            }
            else if (loadFromResources)
            {
                Debug.Log("JsonAnimationPlayer: Loading from Resources: " + resourceFileName);
                PlayDance(resourceFileName);
            }
            else if (animationJson != null)
            {
                Debug.Log("JsonAnimationPlayer: Loading from assigned TextAsset");
                PlayFromJson(animationJson.text);
            }
        }
    }

    private void Update()
    {
        if (!isPlaying || motionData == null || motionData.frames == null || motionData.frames.Length == 0)
        {
            return;
        }

        if (autoDetectAvatarRoot)
        {
            bool needsRootSearch = avatarRoot == null || resolvedBindings.Count == 0 || IsPreviewLikeRoot(avatarRoot);
            if (needsRootSearch && Time.unscaledTime >= nextAvatarRootScanTime)
            {
                nextAvatarRootScanTime = Time.unscaledTime + 1f;

                Transform bestSceneRoot = FindBestRigRootInScene();
                if (bestSceneRoot != null && bestSceneRoot != avatarRoot)
                {
                    int currentScore = avatarRoot != null ? ScoreRigRootCandidate(avatarRoot) : -1;
                    int bestScore = ScoreRigRootCandidate(bestSceneRoot);
                    if (avatarRoot == null || bestScore > currentScore + 2 || (IsPreviewLikeRoot(avatarRoot) && !IsPreviewLikeRoot(bestSceneRoot)))
                    {
                        avatarRoot = bestSceneRoot;
                        rootMotionTarget = null;
                        ResolveBindings();
                    }
                }

                if (avatarRoot == null || resolvedBindings.Count == 0)
                {
                    ResolveBindings();
                }
            }
        }

        float playbackFps = fps > 0f ? fps : (motionData.video_fps > 0f ? motionData.video_fps : 60f);
        frameAccumulator += Time.deltaTime * playbackSpeed * playbackFps;

        int nextFrameIndex = Mathf.FloorToInt(frameAccumulator);
        int frameCount = motionData.frames.Length;

        if (loop)
        {
            nextFrameIndex %= frameCount;
            if (nextFrameIndex < 0)
            {
                nextFrameIndex += frameCount;
            }
        }
        else if (nextFrameIndex >= frameCount)
        {
            nextFrameIndex = frameCount - 1;
            isPlaying = false;
        }

        if (nextFrameIndex != currentFrameIndex)
        {
            currentFrameIndex = nextFrameIndex;
            if (frames != null && currentFrameIndex >= 0 && currentFrameIndex < frames.Length)
            {
                currentRotations = frames[currentFrameIndex] ?? Array.Empty<float>();
            }
            ApplyFrame(currentFrameIndex);
        }
    }

    public float[] getRotations()
    {
        return currentRotations;
    }

    public void PlayDance(string fileName)
    {
        string normalizedFolder = string.IsNullOrWhiteSpace(resourcesFolder) ? string.Empty : resourcesFolder.TrimEnd('/');
        string resourcePath = string.IsNullOrEmpty(normalizedFolder) ? fileName : normalizedFolder + "/" + fileName;
        Debug.Log("JsonAnimationPlayer: Attempting to load JSON file from Resources: " + resourcePath);
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);

        if (jsonFile == null)
        {
            Debug.LogError("Could not find JSON file in Resources: " + resourcePath);
            return;
        }

        PlayFromJson(jsonFile.text);
        Debug.Log("JsonAnimationPlayer: Successfully loaded JSON file, size=" + jsonFile.text.Length);
    }

    public void PlayFromStreamingAssets(string fileName)
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, fileName);
        try
        {
            Debug.Log("JsonAnimationPlayer.PlayFromStreamingAssets: path=" + path);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("JsonAnimationPlayer: Could not find JSON file in StreamingAssets: " + path);
                return;
            }

            string text = System.IO.File.ReadAllText(path);
            Debug.Log("JsonAnimationPlayer: Loaded JSON from StreamingAssets, size=" + text.Length);
            PlayFromJson(text);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to load JSON from StreamingAssets: " + ex.Message);
        }
    }

    public void PlayFromFile(string fullPath)
    {
        try
        {
            Debug.Log("JsonAnimationPlayer.PlayFromFile: fullPath=" + fullPath);
            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogError("JsonAnimationPlayer: Could not find JSON file: " + fullPath);
                return;
            }

            string text = System.IO.File.ReadAllText(fullPath);
            Debug.Log("JsonAnimationPlayer: Loaded JSON from file, size=" + text.Length);
            PlayFromJson(text);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to load JSON file: " + ex.Message);
        }
    }

    public void PlayFromJson(string jsonText)
    {
        Debug.Log("JsonAnimationPlayer.PlayFromJson: jsonText length=" + (jsonText != null ? jsonText.Length : 0));
        motionData = JsonUtility.FromJson<AvatarMotionExport>(jsonText);

        // If standard converted format failed to parse, attempt npz-style runtime conversion
        if (motionData == null || motionData.frames == null || motionData.frames.Length == 0)
        {
            Debug.Log("JsonAnimationPlayer.PlayFromJson: JsonUtility.FromJson produced no frames, attempting npz-style fallback parsing.");
            try
            {
                motionData = ParseNpzStyleJson(jsonText);
                Debug.Log("JsonAnimationPlayer.PlayFromJson: Fallback parsing succeeded. frames=" + (motionData.frames != null ? motionData.frames.Length : 0) + ", bone_names=" + (motionData.bone_names != null ? motionData.bone_names.Length : 0));
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to parse JSON as converted format or npz-style: " + ex.Message);
                return;
            }

            if (motionData == null || motionData.frames == null || motionData.frames.Length == 0)
            {
                Debug.LogError("JSON did not contain any frames after fallback parsing.");
                return;
            }
        }

        frames = BuildLegacyFrames(motionData);
        currentRotations = frames.Length > 0 ? frames[0] : Array.Empty<float>();
        startTime = Time.time;

        if (motionData.video_fps > 0f)
        {
            fps = motionData.video_fps;
        }

        ResolveBindings();
        Debug.Log("JsonAnimationPlayer.PlayFromJson: motionData.frames=" + (motionData.frames != null ? motionData.frames.Length : 0) + ", bone_names=" + (motionData.bone_names != null ? motionData.bone_names.Length : 0) + ", resolvedBindings=" + resolvedBindings.Count);
        if (forceDisableBonePositions && applyBonePositions)
        {
            applyBonePositions = false;
            Debug.LogWarning("JsonAnimationPlayer: Bone positions were force-disabled to prevent rig distortion.");
        }
        frameAccumulator = 0f;
        currentFrameIndex = 0;
        isPlaying = true;
        ApplyFrame(0);
    }

    // Runtime parser for the npz-style JSON (legacy npz-json structure).
    private AvatarMotionExport ParseNpzStyleJson(string jsonText)
    {
        var root = MiniJSON.Deserialize(jsonText) as Dictionary<string, object>;
        if (root == null || !root.ContainsKey("arrays"))
            throw new Exception("Not npz-style JSON (missing 'arrays').");

        var arrays = root["arrays"] as Dictionary<string, object>;
        if (arrays == null)
            throw new Exception("Invalid 'arrays' map.");

        // helper to get array data
        object GetArrayData(string name)
        {
            if (!arrays.ContainsKey(name)) return null;
            var entry = arrays[name] as Dictionary<string, object>;
            if (entry == null || !entry.ContainsKey("data")) return null;
            return entry["data"];
        }

        var frameNamesObj = GetArrayData("frame_names") as List<object>;
        var frameIndicesObj = GetArrayData("frame_indices") as List<object>;
        var pred_cam_t_obj = GetArrayData("pred_cam_t") as List<object>;
        var pred_kp_3d_obj = GetArrayData("pred_keypoints_3d") as List<object>;
        var pred_kp_3d_cam_obj = GetArrayData("pred_keypoints_3d_cam") as List<object>;
        var pred_kp_2d_obj = GetArrayData("pred_keypoints_2d") as List<object>;
        var body_pose_obj = GetArrayData("body_pose_params") as List<object>;
        var video_fps_obj = GetArrayData("video_fps");
        var keypoint_names_obj = GetArrayData("keypoint_names") as List<object>;

        if (frameNamesObj == null || pred_kp_3d_obj == null)
            throw new Exception("Missing required arrays (frame_names or pred_keypoints_3d).");

        int frameCount = frameNamesObj.Count;

        // bone names
        string[] boneNames = null;
        if (keypoint_names_obj != null)
            boneNames = keypoint_names_obj.Select(o => o as string).ToArray();

        // fps
        float fps = 60f;
        if (video_fps_obj != null)
        {
            if (video_fps_obj is List<object> vfList && vfList.Count > 0)
            {
                fps = Convert.ToSingle(vfList[0]);
            }
            else if (video_fps_obj is double d) fps = Convert.ToSingle(d);
            else if (video_fps_obj is long l) fps = l;
        }

        var frames = new FrameData[frameCount];

        // Convert nested arrays
        for (int fi = 0; fi < frameCount; fi++)
        {
            var fd = new FrameData();
            fd.frame_index = frameIndicesObj != null ? Convert.ToInt32(frameIndicesObj[fi]) : fi;
            fd.frame_name = frameNamesObj[fi] as string;

            // pred_cam_t
            fd.pred_cam_t_centered = TryExtractFloatArrayFromList(pred_cam_t_obj, fi, 3);

            // body_pose_params
            fd.body_pose_params = TryExtractFloatArrayFromList(body_pose_obj, fi, -1);

            // bones
            var bonesList = new List<BoneSample>();

            var frameKp3d = pred_kp_3d_obj[fi] as List<object>;
            var frameKp3dCam = pred_kp_3d_cam_obj != null ? pred_kp_3d_cam_obj[fi] as List<object> : null;
            var frameKp2d = pred_kp_2d_obj != null ? pred_kp_2d_obj[fi] as List<object> : null;

            int boneCount = frameKp3d != null ? frameKp3d.Count : 0;
            for (int bi = 0; bi < boneCount; bi++)
            {
                var bs = new BoneSample();
                bs.index = bi;
                bs.name = boneNames != null && bi < boneNames.Length ? boneNames[bi] : "bone_" + bi;

                // point_3d
                bs.point_3d = TryExtractFloatArrayFromNested(frameKp3d, bi, 3);
                // point_3d_cam
                if (frameKp3dCam != null)
                    bs.point_3d_cam = TryExtractFloatArrayFromNested(frameKp3dCam, bi, 3);
                // point_2d
                if (frameKp2d != null)
                    bs.point_2d = TryExtractFloatArrayFromNested(frameKp2d, bi, 2);

                bonesList.Add(bs);
            }

            fd.bones = bonesList.ToArray();
            frames[fi] = fd;
        }

        var export = new AvatarMotionExport();
        export.source = root.ContainsKey("source") ? root["source"] as string : null;
        export.source_title = ExtractStringFromArrayEntry(arrays, "source_title");
        export.object_id = root.ContainsKey("object_id") && root["object_id"] is long ? Convert.ToInt32(root["object_id"]) : 0;
        export.video_fps = fps;
        export.bone_names = boneNames ?? new string[0];
        export.frames = frames;

        return export;
    }

    private float[] TryExtractFloatArrayFromList(List<object> arrayOfFrames, int frameIndex, int expectedLen)
    {
        if (arrayOfFrames == null || frameIndex >= arrayOfFrames.Count) return null;
        var frameEntry = arrayOfFrames[frameIndex];
        if (frameEntry is List<object> lst && lst.Count >= (expectedLen > 0 ? expectedLen : 1))
        {
            if (expectedLen > 0 && lst.Count >= expectedLen && lst[0] is double)
            {
                // flat numeric array
                return lst.Select(o => Convert.ToSingle(o)).ToArray();
            }
            else if (lst[0] is List<object>)
            {
                // it's an array of arrays (handled elsewhere)
                return null;
            }
        }
        return null;
    }

    private float[] TryExtractFloatArrayFromNested(List<object> nestedList, int index, int expectedLen)
    {
        if (nestedList == null || index >= nestedList.Count) return null;
        var entry = nestedList[index] as List<object>;
        if (entry == null) return null;
        var outArr = new float[Math.Max(0, expectedLen)];
        int len = expectedLen > 0 ? Math.Min(expectedLen, entry.Count) : entry.Count;
        var result = new float[len];
        for (int i = 0; i < len; i++) result[i] = Convert.ToSingle(entry[i]);
        return result;
    }

    private float[][] BuildLegacyFrames(AvatarMotionExport export)
    {
        if (export == null || export.frames == null || export.frames.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        float[][] output = new float[export.frames.Length][];
        for (int i = 0; i < export.frames.Length; i++)
        {
            FrameData frame = export.frames[i];
            float[] source = frame != null ? frame.body_pose_params : null;
            if (source == null || source.Length == 0)
            {
                source = FlattenBoneRotations(frame);
            }

            output[i] = ApplyLegacyOrientationFix(source);
        }

        return output;
    }

    private float[] FlattenBoneRotations(FrameData frame)
    {
        if (frame == null || frame.bones == null || frame.bones.Length == 0)
        {
            return Array.Empty<float>();
        }

        List<float> flattened = new List<float>();
        for (int i = 0; i < frame.bones.Length; i++)
        {
            BoneSample bone = frame.bones[i];
            if (bone == null)
            {
                continue;
            }

            if (bone.rotation != null && bone.rotation.Length > 0)
            {
                flattened.AddRange(bone.rotation);
            }
            else if (bone.rotation_cam != null && bone.rotation_cam.Length > 0)
            {
                flattened.AddRange(bone.rotation_cam);
            }
        }

        return flattened.Count > 0 ? flattened.ToArray() : Array.Empty<float>();
    }

    private float[] ApplyLegacyOrientationFix(float[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<float>();
        }

        float[] result = new float[source.Length];
        Array.Copy(source, result, source.Length);
        return result;
    }

    private static string ExtractStringFromArrayEntry(Dictionary<string, object> arrays, string name)
    {
        if (arrays == null || !arrays.ContainsKey(name)) return null;
        var entry = arrays[name] as Dictionary<string, object>;
        if (entry == null || !entry.ContainsKey("data")) return null;

        object data = entry["data"];
        if (data is string s)
        {
            return s;
        }

        if (data is List<object> list && list.Count > 0)
        {
            return list[0] as string;
        }

        return data != null ? data.ToString() : null;
    }

    // Minimal JSON parser so this script stays self-contained in Unity.
    private static class MiniJSON
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return new Parser(json).ParseValue();
        }

        private sealed class Parser
        {
            private readonly string json;
            private int index;

            public Parser(string json)
            {
                this.json = json;
                this.index = 0;
            }

            public object ParseValue()
            {
                EatWhitespace();
                if (index >= json.Length) return null;

                char c = json[index];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': return ParseLiteral("true", true);
                    case 'f': return ParseLiteral("false", false);
                    case 'n': return ParseLiteral("null", null);
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                index++; // {
                EatWhitespace();

                while (index < json.Length && json[index] != '}')
                {
                    string key = ParseString();
                    EatWhitespace();
                    if (index < json.Length && json[index] == ':') index++;
                    object value = ParseValue();
                    table[key] = value;
                    EatWhitespace();
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                        EatWhitespace();
                    }
                }

                if (index < json.Length && json[index] == '}') index++;
                return table;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                index++; // [
                EatWhitespace();

                while (index < json.Length && json[index] != ']')
                {
                    object value = ParseValue();
                    list.Add(value);
                    EatWhitespace();
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                        EatWhitespace();
                    }
                }

                if (index < json.Length && json[index] == ']') index++;
                return list;
            }

            private string ParseString()
            {
                if (index >= json.Length || json[index] != '"') return null;
                index++; // opening quote

                var sb = new System.Text.StringBuilder();
                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"') break;
                    if (c == '\\' && index < json.Length)
                    {
                        char esc = json[index++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (index + 4 <= json.Length)
                                {
                                    string hex = json.Substring(index, 4);
                                    sb.Append((char)Convert.ToInt32(hex, 16));
                                    index += 4;
                                }
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            private object ParseNumber()
            {
                int start = index;
                while (index < json.Length)
                {
                    char c = json[index];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                        index++;
                    else
                        break;
                }

                string token = json.Substring(start, index - start);
                if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
                {
                    double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d);
                    return d;
                }

                long.TryParse(token, out long l);
                return l;
            }

            private object ParseLiteral(string literal, object value)
            {
                if (json.Substring(index).StartsWith(literal))
                {
                    index += literal.Length;
                    return value;
                }
                return null;
            }

            private void EatWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            }
        }
    }

    public void StopDance()
    {
        isPlaying = false;
    }

    public void RestartDance()
    {
        if (motionData == null || motionData.frames == null || motionData.frames.Length == 0)
        {
            return;
        }

        frameAccumulator = 0f;
        currentFrameIndex = 0;
        isPlaying = true;
        ApplyFrame(0);
    }

    public FrameData GetFrameByName(string frameName)
    {
        if (motionData == null || motionData.frames == null)
        {
            return null;
        }

        for (int i = 0; i < motionData.frames.Length; i++)
        {
            if (motionData.frames[i] != null && motionData.frames[i].frame_name == frameName)
            {
                return motionData.frames[i];
            }
        }

        return null;
    }

    private void ResolveBindings()
    {
        resolvedBindings.Clear();
        bindingSourceRestPositions.Clear();
        bindingTargetRestLocalPositions.Clear();

        if (autoDetectAvatarRoot)
        {
            TryAutoDetectAvatarRoot();
        }

        if (boneBindings != null)
        {
            for (int i = 0; i < boneBindings.Length; i++)
            {
                BoneBinding binding = boneBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.jsonBoneName))
                {
                    continue;
                }

                if (binding.target == null && avatarRoot != null)
                {
                    binding.target = FindDeepChildFlexible(avatarRoot, binding.jsonBoneName);
                }

                if (binding.target != null)
                {
                    resolvedBindings[binding.jsonBoneName] = binding;
                }
            }
        }

        if (avatarRoot == null)
        {
            avatarRoot = this.transform;
        }

        if (avatarRoot != null && motionData.bone_names != null)
        {
            for (int i = 0; i < motionData.bone_names.Length; i++)
            {
                string boneName = motionData.bone_names[i];
                if (resolvedBindings.ContainsKey(boneName))
                {
                    continue;
                }

                Transform target = FindMappedBone(avatarRoot, boneName);
                if (target != null)
                {
                    resolvedBindings[boneName] = new BoneBinding
                    {
                        jsonBoneName = boneName,
                        target = target,
                        useLocalPosition = true,
                        positionScale = Vector3.one,
                        positionOffset = Vector3.zero
                    };

                    CacheRestPose(boneName, target);
                }
            }
        }

        if (rootMotionTarget == null)
        {
            rootMotionTarget = FindBestRootMotionTarget();
            if (rootMotionTarget != null)
            {
                Debug.Log("JsonAnimationPlayer.ResolveBindings: auto-selected rootMotionTarget = " + rootMotionTarget.name);
            }
        }
        Debug.Log("JsonAnimationPlayer.ResolveBindings: resolvedBindings=" + resolvedBindings.Count + ", avatarRoot=" + (avatarRoot != null) + ", bone_names=" + (motionData.bone_names != null ? motionData.bone_names.Length : 0));
        // Print a small sample of bone name lookups to help debug binding mismatches
        try
        {
            if (motionData != null && motionData.bone_names != null && avatarRoot != null)
            {
                int sample = Math.Min(12, motionData.bone_names.Length);
                for (int i = 0; i < sample; i++)
                {
                    string bn = motionData.bone_names[i];
                    Transform found = FindDeepChildFlexible(avatarRoot, bn);
                    Debug.Log("JsonAnimationPlayer.ResolveBindings: sample bone[" + i + "]='" + bn + "' found=" + (found != null));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("JsonAnimationPlayer.ResolveBindings: sample lookup failed: " + ex.Message);
        }

        // If very few bindings were found, dump more diagnostics about the rig
        if (resolvedBindings.Count < 8 && avatarRoot != null)
        {
            try
            {
                Debug.Log("JsonAnimationPlayer.ResolveBindings: Low binding count — listing avatarRoot descendants (name -> normalized)");
                var q = new Queue<Transform>();
                q.Enqueue(avatarRoot);
                int printed = 0;
                while (q.Count > 0 && printed < 80)
                {
                    var t = q.Dequeue();
                    string norm = NormalizeBoneName(t.name);
                    Debug.Log("  -> '" + t.name + "' -> '" + norm + "'");
                    printed++;
                    for (int c = 0; c < t.childCount; c++) q.Enqueue(t.GetChild(c));
                }

                if (motionData != null && motionData.bone_names != null)
                {
                    Debug.Log("JsonAnimationPlayer.ResolveBindings: Sample JSON bone names (normalized)");
                    int s2 = Math.Min(30, motionData.bone_names.Length);
                    for (int i = 0; i < s2; i++)
                    {
                        Debug.Log("  json['" + motionData.bone_names[i] + "'] -> '" + NormalizeBoneName(motionData.bone_names[i]) + "'");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("JsonAnimationPlayer.ResolveBindings: extra diagnostics failed: " + ex.Message);
            }
        }
    }

    private bool TryAutoDetectAvatarRoot()
    {
        Transform candidate = null;

        if (avatarRoot != null)
        {
            if (HasAnyDescendantBones(avatarRoot))
            {
                return true;
            }

            candidate = FindFirstLikelyRigRoot(avatarRoot);
            if (candidate != null && candidate != avatarRoot)
            {
                avatarRoot = candidate;
                Debug.Log("JsonAnimationPlayer: Auto-detected avatarRoot = " + avatarRoot.name);
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(autoDetectAvatarRootName))
        {
            candidate = FindDeepChildFlexible(transform, autoDetectAvatarRootName);
        }

        if (candidate == null)
        {
            candidate = FindFirstLikelyRigRoot(transform);
        }

        if (candidate == null)
        {
            candidate = FindBestRigRootInScene();
        }

        if (candidate != null && candidate != avatarRoot)
        {
            avatarRoot = candidate;
            Debug.Log("JsonAnimationPlayer: Auto-detected avatarRoot = " + avatarRoot.name);
            return true;
        }

        return false;
    }

    private static bool IsPreviewLikeRoot(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        string normalized = NormalizeBoneName(root.name);
        return normalized.Contains("preview") || normalized.Contains("motiontracking");
    }

    private Transform FindBestRigRootInScene()
    {
        if (motionData == null || motionData.bone_names == null || motionData.bone_names.Length == 0)
        {
            return null;
        }

        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        Transform bestRoot = null;
        int bestScore = 0;

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform root = rootObjects[i] != null ? rootObjects[i].transform : null;
            if (root == null)
            {
                continue;
            }

            int score = ScoreRigRootCandidate(root);

            if (score > bestScore)
            {
                bestScore = score;
                bestRoot = root;
            }
        }

        if (bestRoot != null && bestScore > 0)
        {
            Debug.Log("JsonAnimationPlayer: Scene fallback avatarRoot = " + bestRoot.name + " (matches=" + bestScore + ")");
        }

        return bestRoot;
    }

    private int ScoreRigRootCandidate(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int score = CountMatchingBones(root, 16);
        string normalizedName = NormalizeBoneName(root.name);

        if (HasVisibleAvatarGeometry(root))
        {
            score += 8;
        }

        if (normalizedName.Contains("avatar") || normalizedName.Contains("character"))
        {
            score += 4;
        }

        if (normalizedName.Contains("armature") || normalizedName.Contains("skeleton"))
        {
            score += 2;
        }

        if (normalizedName.Contains("preview") || normalizedName.Contains("motiontracking") || normalizedName.Contains("tracker"))
        {
            score -= 6;
        }

        return score;
    }

    private static bool HasVisibleAvatarGeometry(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
        {
            return true;
        }

        if (root.GetComponentInChildren<MeshRenderer>(true) != null)
        {
            return true;
        }

        return root.GetComponentInChildren<Animator>(true) != null;
    }

    private int CountMatchingBones(Transform root, int sampleLimit)
    {
        if (root == null || motionData == null || motionData.bone_names == null)
        {
            return 0;
        }

        int matches = 0;
        int limit = Mathf.Min(sampleLimit, motionData.bone_names.Length);
        for (int i = 0; i < limit; i++)
        {
            if (FindDeepChildFlexible(root, motionData.bone_names[i]) != null)
            {
                matches++;
            }
        }

        return matches;
    }

    private bool HasAnyDescendantBones(Transform root)
    {
        if (root == null || motionData == null || motionData.bone_names == null || motionData.bone_names.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < motionData.bone_names.Length; i++)
        {
            if (FindDeepChildFlexible(root, motionData.bone_names[i]) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindFirstLikelyRigRoot(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        string normalizedParent = NormalizeBoneName(parent.name);
        if (normalizedParent == "armature" || normalizedParent == "skeleton")
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string normalizedChild = NormalizeBoneName(child.name);
            if (normalizedChild == "armature" || normalizedChild == "skeleton")
            {
                return child;
            }

            Transform descendant = FindFirstLikelyRigRoot(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void ApplyFrame(int frameIndex)
    {
        FrameData frame = motionData.frames[frameIndex];
        if (frame == null)
        {
            return;
        }

        if (applyRootMotion && TryReadVector3(frame.pred_cam_t_centered, out Vector3 rootPosition))
        {
            if (rootMotionTarget == null)
            {
                rootMotionTarget = FindBestRootMotionTarget();
            }

            if (rootMotionTarget != null)
            {
            rootMotionTarget.localPosition = rootPosition * uniformScale + globalPositionOffset;
            }
        }

        if (frame.bones == null)
        {
            return;
        }

        var boneMap = new Dictionary<string, BoneSample>(StringComparer.OrdinalIgnoreCase);
        for (int k = 0; k < frame.bones.Length; k++)
        {
            BoneSample mappedBone = frame.bones[k];
            if (mappedBone != null && !string.IsNullOrWhiteSpace(mappedBone.name))
            {
                boneMap[mappedBone.name] = mappedBone;
            }
        }

        for (int i = 0; i < frame.bones.Length; i++)
        {
            BoneSample bone = frame.bones[i];
            if (bone == null || string.IsNullOrWhiteSpace(bone.name))
            {
                continue;
            }

            if (!resolvedBindings.TryGetValue(bone.name, out BoneBinding binding) || binding.target == null)
            {
                continue;
            }

            float[] source = usePoint3D ? bone.point_3d : bone.point_3d_cam;
            if (!TryReadVector3(source, out Vector3 bonePosition))
            {
                continue;
            }

            if (applyBonePositions)
            {
                Vector3 finalPosition = ComputeRelativeBonePosition(bone.name, bonePosition, binding);
                if (binding.useLocalPosition)
                {
                    binding.target.localPosition = finalPosition;
                }
                else
                {
                    binding.target.position = finalPosition;
                }
            }

            if (applyBoneRotations)
            {
                bool appliedRotation = false;

                // 1) Prefer source quaternion if requested and available
                if (useSourceQuaternions && bone.rotation != null && bone.rotation.Length >= 4 && binding.preferSourceQuaternion)
                {
                    try
                    {
                        Quaternion q = new Quaternion(bone.rotation[0], bone.rotation[1], bone.rotation[2], bone.rotation[3]);
                        q = Quaternion.Euler(globalRotationOffsetEuler) * q;
                        q = q * Quaternion.Euler(binding.rotationOffsetEuler);
                        if (binding.useLocalRotation)
                            binding.target.localRotation = q;
                        else
                            binding.target.rotation = q;
                        appliedRotation = true;
                    }
                    catch (Exception)
                    {
                        appliedRotation = false;
                    }
                }

                // 2) Compute look-rotation from this bone to a child bone, explicit or inferred
                if (!appliedRotation)
                {
                    IEnumerable<string> childCandidates = GetAutoChildBoneCandidates(bone.name, binding.childJsonBoneName);
                    foreach (string childName in childCandidates)
                    {
                        if (!boneMap.TryGetValue(childName, out BoneSample childBone))
                        {
                            continue;
                        }

                        float[] srcA = usePoint3D ? bone.point_3d : bone.point_3d_cam;
                        float[] srcB = usePoint3D ? childBone.point_3d : childBone.point_3d_cam;
                        if (TryReadVector3(srcA, out Vector3 posA) && TryReadVector3(srcB, out Vector3 posB))
                        {
                            Vector3 dir = posB - posA;
                            if (dir.sqrMagnitude > 1e-6f)
                        {
                                Quaternion look = Quaternion.LookRotation(dir.normalized, defaultUp);
                                look = Quaternion.Euler(globalRotationOffsetEuler) * look;
                                look = look * Quaternion.Euler(binding.rotationOffsetEuler);
                                if (binding.useLocalRotation)
                                    binding.target.localRotation = look;
                                else
                                    binding.target.rotation = look;
                                appliedRotation = true;
                                break;
                            }
                        }
                    }
                }

                // 3) Fallback - use rotation_cam if available and preference allows
                if (!appliedRotation && bone.rotation_cam != null && bone.rotation_cam.Length >= 4 && useSourceQuaternions && !binding.preferSourceQuaternion)
                {
                    try
                    {
                        Quaternion q2 = new Quaternion(bone.rotation_cam[0], bone.rotation_cam[1], bone.rotation_cam[2], bone.rotation_cam[3]);
                        q2 = Quaternion.Euler(globalRotationOffsetEuler) * q2;
                        q2 = q2 * Quaternion.Euler(binding.rotationOffsetEuler);
                        if (binding.useLocalRotation)
                            binding.target.localRotation = q2;
                        else
                            binding.target.rotation = q2;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }

    private static bool TryReadVector3(float[] values, out Vector3 vector)
    {
        if (values != null && values.Length >= 3)
        {
            vector = new Vector3(values[0], values[1], values[2]);
            return true;
        }

        vector = default;
        return false;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform result = FindDeepChild(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Transform FindDeepChildFlexible(Transform parent, string targetName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        string normalizedTarget = NormalizeBoneName(targetName);
        string shortTarget = NormalizeBoneName(GetShortBoneName(targetName));

        if (NameMatches(parent.name, normalizedTarget, shortTarget))
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform result = FindDeepChildFlexible(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private Transform FindBestRootMotionTarget()
    {
        if (resolvedBindings.TryGetValue("pelvis", out BoneBinding pelvisBinding) && pelvisBinding != null && pelvisBinding.target != null)
        {
            return pelvisBinding.target;
        }

        string[] candidateNames = new[] { "hips", "pelvis", "spine", "spine1", "chest", "upperchest", "root" };
        if (avatarRoot != null)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                Transform target = FindDeepChildFlexible(avatarRoot, candidateNames[i]);
                if (IsCentralRootMotionCandidate(target))
                {
                    return target;
                }
            }

            return avatarRoot;
        }

        return transform;
    }

    private static bool IsCentralRootMotionCandidate(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string normalized = NormalizeBoneName(target.name);
        if (normalized.Contains("left") || normalized.Contains("right"))
        {
            return false;
        }

        return normalized == "hips" || normalized == "pelvis" || normalized == "spine" || normalized.StartsWith("spine") || normalized == "chest" || normalized == "upperchest" || normalized == "root";
    }

    private static IEnumerable<string> GetAutoChildBoneCandidates(string boneName, string explicitChildName = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitChildName))
        {
            yield return explicitChildName;
            yield break;
        }

        string normalized = NormalizeBoneName(boneName);
        switch (normalized)
        {
            case "pelvis":
            case "hip":
            case "hips":
                yield return "spine";
                yield return "chest";
                yield return "neck";
                yield break;
            case "spine":
            case "spine1":
            case "spine2":
            case "chest":
            case "upperchest":
                yield return "neck";
                yield return "head";
                yield break;
            case "neck":
                yield return "head";
                yield break;
            case "leftshoulder":
                yield return "leftelbow";
                yield return "leftforearm";
                yield return "lefthand";
                yield break;
            case "rightshoulder":
                yield return "rightelbow";
                yield return "rightforearm";
                yield return "righthand";
                yield break;
            case "leftelbow":
                yield return "lefthand";
                yield return "leftwrist";
                yield return "leftforearm";
                yield break;
            case "rightelbow":
                yield return "righthand";
                yield return "rightwrist";
                yield return "rightforearm";
                yield break;
            case "lefthip":
                yield return "leftknee";
                yield return "leftupleg";
                yield break;
            case "righthip":
                yield return "rightknee";
                yield return "rightupleg";
                yield break;
            case "leftknee":
                yield return "leftankle";
                yield return "leftfoot";
                yield break;
            case "rightknee":
                yield return "rightankle";
                yield return "rightfoot";
                yield break;
            case "leftankle":
                yield return "leftfoot";
                yield return "leftheel";
                yield return "leftbigtoetip";
                yield break;
            case "rightankle":
                yield return "rightfoot";
                yield return "rightheel";
                yield return "rightbigtoetip";
                yield break;
        }
    }

    private Transform FindMappedBone(Transform parent, string jsonBoneName)
    {
        // Fast path: direct flexible deep search and aliases
        Transform target = FindDeepChildFlexible(parent, jsonBoneName);
        if (target != null)
            return target;

        foreach (string alias in GetBoneAliases(jsonBoneName))
        {
            target = FindDeepChildFlexible(parent, alias);
            if (target != null)
                return target;
        }

        // Otherwise, score all descendants and pick best candidate
        string normTarget = NormalizeBoneName(jsonBoneName);
        float bestScore = 0f;
        Transform bestTransform = null;
        var candidates = new List<(float score, Transform t)>();

        var q = new Queue<Transform>();
        q.Enqueue(parent);
        while (q.Count > 0)
        {
            var t = q.Dequeue();
            float score = ComputeNameMatchScore(t.name, jsonBoneName);
            if (score > 0f)
            {
                candidates.Add((score, t));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTransform = t;
                }
            }

            for (int i = 0; i < t.childCount; i++) q.Enqueue(t.GetChild(i));
        }

        // Log top candidates to help diagnostics
        if (candidates.Count > 0)
        {
            var top = candidates.OrderByDescending(x => x.score).Take(3).ToArray();
            string log = "JsonAnimationPlayer.FindMappedBone: top candidates for json='" + jsonBoneName + "': ";
            for (int i = 0; i < top.Length; i++)
            {
                log += $"('{top[i].t.name}', score={top[i].score:F2})" + (i + 1 < top.Length ? ", " : string.Empty);
            }
            Debug.Log(log);
        }

        // require a reasonable confidence to accept automatically
        const float acceptanceThreshold = 0.55f;
        if (bestTransform != null && bestScore >= acceptanceThreshold)
        {
            Debug.Log($"JsonAnimationPlayer.FindMappedBone: auto-mapped json='{jsonBoneName}' -> '{bestTransform.name}' (score={bestScore:F2})");
            return bestTransform;
        }

        if (bestTransform != null)
        {
            Debug.Log($"JsonAnimationPlayer.FindMappedBone: best candidate for json='{jsonBoneName}' was '{bestTransform.name}' (score={bestScore:F2}) but below threshold");
        }

        return null;
    }

    private static float ComputeNameMatchScore(string candidateName, string jsonBoneName)
    {
        if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(jsonBoneName)) return 0f;

        string normCandidate = NormalizeBoneName(candidateName);
        string normTarget = NormalizeBoneName(jsonBoneName);

        // exact match
        if (normCandidate == normTarget) return 1f;

        // alias match
        foreach (var alias in GetBoneAliases(jsonBoneName))
        {
            if (NormalizeBoneName(alias) == normCandidate) return 0.95f;
        }

        // contains / startswith / endswith
        if (normCandidate.Contains(normTarget) || normTarget.Contains(normCandidate)) return 0.8f;
        if (normCandidate.StartsWith(normTarget) || normCandidate.EndsWith(normTarget)) return 0.75f;

        // token overlap score
        var tokA = TokenizeName(candidateName).Distinct().ToArray();
        var tokB = TokenizeName(jsonBoneName).Distinct().ToArray();
        if (tokA.Length > 0 && tokB.Length > 0)
        {
            int inter = tokA.Intersect(tokB, StringComparer.OrdinalIgnoreCase).Count();
            int uni = tokA.Union(tokB, StringComparer.OrdinalIgnoreCase).Count();
            if (inter > 0 && uni > 0)
            {
                float overlap = (float)inter / (float)uni; // 0..1
                // scale token overlap into score range
                return 0.3f + 0.6f * overlap; // base 0.3 up to 0.9
            }
        }

        // small fallback: fuzzy edit distance normalized (cheap implementation)
        int ed = LevenshteinDistance(normCandidate, normTarget);
        int max = Math.Max(normCandidate.Length, normTarget.Length);
        if (max > 0)
        {
            float norm = 1f - (float)ed / (float)max; // 0..1
            if (norm > 0.5f) return 0.2f + 0.6f * norm; // 0.2..0.8 depending
        }

        return 0f;
    }

    private static IEnumerable<string> TokenizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) yield break;
        // split on non-alphanumeric and also capture letter/number runs
        foreach (Match m in Regex.Matches(name, "[A-Za-z0-9]+"))
        {
            string t = m.Value.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(t)) yield return t;
        }
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (s == null) s = string.Empty;
        if (t == null) t = string.Empty;
        int n = s.Length;
        int m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static IEnumerable<string> GetBoneAliases(string jsonBoneName)
    {
        if (string.IsNullOrWhiteSpace(jsonBoneName))
        {
            yield break;
        }

        string n = NormalizeBoneName(jsonBoneName);
        switch (n)
        {
            case "nose":
                yield return "head";
                yield return "headtop";
                yield return "headtopend";
                yield break;
            case "lefteye":
            case "righteye":
            case "leftear":
            case "rightear":
                yield return "head";
                yield break;
            
            case "pelvis":
            case "hip":
            case "hips":
                yield return "pelvis";
                yield return "hip";
                yield return "hips";
                yield return "root";
                yield return "mixamorig:hips";
                yield break;
            case "spine":
            case "spine1":
            case "spine2":
            case "chest":
            case "upperchest":
                yield return "spine";
                yield return "chest";
                yield return "upperchest";
                yield return "thorax";
                yield return "mixamorig:spine";
                yield break;
            case "neck":
                yield return "neck";
                yield return "neck1";
                yield return "cervical";
                yield break;
            case "head":
                yield return "head";
                yield return "headtop";
                yield return "mixamorig:head";
                yield break;
            case "leftshoulder":
            case "rightshoulder":
                yield return "clavicle";
                yield return "shoulder";
                yield return "upperarm";
                yield break;
            case "leftelbow":
            case "rightelbow":
                yield return "elbow";
                yield return "lowerarm";
                yield return "forearm";
                yield break;
            case "lefthand":
            case "righthand":
                yield return "hand";
                yield return "wrist";
                yield break;
            case "leftknee":
            case "rightknee":
                yield return "knee";
                yield return "calf";
                yield return "lowerleg";
                yield break;
            case "leftankle":
            case "rightankle":
                yield return "ankle";
                yield return "foot";
                yield break;
        }
    }

    private void CacheRestPose(string boneName, Transform target)
    {
        if (string.IsNullOrWhiteSpace(boneName) || target == null)
        {
            return;
        }

        if (!bindingTargetRestLocalPositions.ContainsKey(boneName))
        {
            bindingTargetRestLocalPositions[boneName] = target.localPosition;
        }
    }

    private Vector3 ComputeRelativeBonePosition(string boneName, Vector3 currentSource, BoneBinding binding)
    {
        if (!bindingSourceRestPositions.TryGetValue(boneName, out Vector3 sourceRest))
        {
            bindingSourceRestPositions[boneName] = currentSource;
            sourceRest = currentSource;
        }

        if (!bindingTargetRestLocalPositions.TryGetValue(boneName, out Vector3 targetRest))
        {
            targetRest = binding.target != null ? binding.target.localPosition : Vector3.zero;
            bindingTargetRestLocalPositions[boneName] = targetRest;
        }

        Vector3 delta = (currentSource - sourceRest) * uniformScale;
        Vector3 finalPosition = Vector3.Scale(targetRest + delta, binding.positionScale) + binding.positionOffset + globalPositionOffset;
        return finalPosition;
    }

    private static bool NameMatches(string candidate, string normalizedTarget, string shortTarget)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string normalizedCandidate = NormalizeBoneName(candidate);
        string shortCandidate = NormalizeBoneName(GetShortBoneName(candidate));

        // exact or short-name matches
        if (normalizedCandidate == normalizedTarget
            || normalizedCandidate == shortTarget
            || shortCandidate == normalizedTarget
            || shortCandidate == shortTarget)
            return true;

        // fuzzy contains-based matches to handle naming variations (e.g. lefthip vs thigh_l)
        if (!string.IsNullOrEmpty(normalizedCandidate) && !string.IsNullOrEmpty(normalizedTarget))
        {
            if (normalizedCandidate.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedCandidate))
                return true;
        }

        if (!string.IsNullOrEmpty(shortCandidate) && !string.IsNullOrEmpty(shortTarget))
        {
            if (shortCandidate.Contains(shortTarget) || shortTarget.Contains(shortCandidate))
                return true;
        }

        return false;
    }

    private static string GetShortBoneName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        int index = name.LastIndexOfAny(new[] { ':', '|', '/', '\\', '.' });
        return index >= 0 && index + 1 < name.Length ? name.Substring(index + 1) : name;
    }

    private static string NormalizeBoneName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = char.ToLowerInvariant(name[i]);
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
