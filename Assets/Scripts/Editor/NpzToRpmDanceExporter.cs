using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Publishes npz-json from StreamingAssets into DancesLegacyNotInBuild (not bundled in WebGL).
/// </summary>
public static class NpzToRpmDanceExporter
{
    const string NpzSourceDefault = "Assets/StreamingAssets/source.json";
    const string OutputDefault = "Assets/DancesLegacyNotInBuild/Dances/ConvertedDance.json";

    const string AtundaOneSource = "Assets/StreamingAssets/atunda/1.json";
    const string AtundaOneOutput = "Assets/DancesLegacyNotInBuild/Dances/1.json";

    [MenuItem("UPose/Build dance from StreamingAssets")]
    public static void BuildDanceDefault()
    {
        ConvertNpzToRpmDance(NpzSourceDefault, OutputDefault, 30f);
    }

    [MenuItem("UPose/Build dance from atunda/1.json")]
    public static void BuildAtundaOneDance()
    {
        ConvertNpzToRpmDance(AtundaOneSource, AtundaOneOutput, 30f);
    }

    [MenuItem("UPose/Convert npz-json to RPM Dance JSON")]
    public static void ConvertDefault()
    {
        ConvertNpzToRpmDance(NpzSourceDefault, OutputDefault, 30f);
    }

    [MenuItem("UPose/Convert npz-json (pick files)...")]
    public static void ConvertWithPicker()
    {
        string input = EditorUtility.OpenFilePanel("npz-json source", Application.streamingAssetsPath, "json");
        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        string output = EditorUtility.SaveFilePanel("RPM dance output", "Assets/DancesLegacyNotInBuild/Dances", "ConvertedDance", "json");
        if (string.IsNullOrEmpty(output))
        {
            return;
        }

        string projectRelative = ToProjectRelative(output);
        ConvertNpzToRpmDance(ToProjectRelative(input), projectRelative, 30f);
    }

    public static void ConvertNpzToRpmDance(string inputProjectPath, string outputProjectPath, float outputFps)
    {
        if (!File.Exists(inputProjectPath))
        {
            EditorUtility.DisplayDialog("npz → RPM", "Input not found:\n" + inputProjectPath, "OK");
            return;
        }

        try
        {
            string json = File.ReadAllText(inputProjectPath);
            NpzMotionClip clip = NpzJsonMotionParser.Parse(json);
            float[][] frames;
            string legacyRef = RpmDanceConverter.ResolveLegacyReferencePath(
                clip.source,
                Application.streamingAssetsPath);

            if (!string.IsNullOrEmpty(legacyRef))
            {
                float[][] legacyFrames = JsonConvert.DeserializeObject<float[][]>(File.ReadAllText(legacyRef));
                int outCount = clip.frames.Length;
                if (outputFps > 0f && clip.fps > outputFps + 0.5f)
                {
                    outCount = Mathf.Max(1, Mathf.RoundToInt(clip.frames.Length * outputFps / clip.fps));
                }

                legacyFrames = RpmDanceConverter.ResampleToFrameCount(legacyFrames, outCount);
                frames = AttachRootMotion(legacyFrames, clip.frames);
                Debug.Log($"Build dance: timed to npz ({outCount} frames), rotations from calibrated legacy {legacyRef}");
            }
            else
            {
                frames = RpmDanceConverter.ToLegacyFrames(clip);
                if (outputFps > 0f && clip.fps > outputFps + 0.5f)
                {
                    frames = Resample(frames, clip.fps, outputFps);
                }
            }

            string outJson = JsonConvert.SerializeObject(frames, Formatting.None);
            Directory.CreateDirectory(Path.GetDirectoryName(outputProjectPath) ?? "Assets/DancesLegacyNotInBuild/Dances");
            File.WriteAllText(outputProjectPath, outJson);
            AssetDatabase.Refresh();

            string mode = !string.IsNullOrEmpty(legacyRef)
                ? "Timed from your npz; bone rotations from calibrated legacy (same capture)."
                : "Converted with keypoint IK (arms may need calibration).";

            EditorUtility.DisplayDialog(
                "npz → RPM",
                $"Wrote {frames.Length} frames × {RpmDanceConverter.FrameWithRootCount} floats (19 rotations + pred_cam_t root)\n{outputProjectPath}\n\n{mode}\n\nPlay with PlayDance(\"<name>\") — no .json extension.",
                "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("npz → RPM failed", ex.Message, "OK");
        }
    }

    static float[][] Resample(float[][] frames, float sourceFps, float targetFps)
    {
        if (frames == null || frames.Length == 0)
        {
            return frames;
        }

        int outCount = Mathf.Max(1, Mathf.RoundToInt(frames.Length * targetFps / sourceFps));
        float[][] output = new float[outCount][];
        for (int i = 0; i < outCount; i++)
        {
            float t = i * (frames.Length - 1) / Mathf.Max(1f, outCount - 1f);
            int i0 = Mathf.FloorToInt(t);
            int i1 = Mathf.Min(i0 + 1, frames.Length - 1);
            float a = t - i0;
            output[i] = LerpFrame(frames[i0], frames[i1], a);
        }

        return output;
    }

    static float[] LerpFrame(float[] a, float[] b, float t)
    {
        int n = Mathf.Min(a?.Length ?? 0, b?.Length ?? 0);
        float[] o = new float[n];
        for (int i = 0; i < n; i++)
        {
            o[i] = Mathf.Lerp(a[i], b[i], t);
        }

        return o;
    }

    static float[][] AttachRootMotion(float[][] rotationFrames, NpzMotionFrame[] sourceFrames)
    {
        if (rotationFrames == null || sourceFrames == null || sourceFrames.Length == 0)
        {
            return rotationFrames;
        }

        float[][] output = new float[rotationFrames.Length][];
        for (int i = 0; i < rotationFrames.Length; i++)
        {
            float t = i * (sourceFrames.Length - 1) / Mathf.Max(1f, rotationFrames.Length - 1f);
            int sourceIndex = Mathf.Clamp(Mathf.RoundToInt(t), 0, sourceFrames.Length - 1);
            output[i] = RpmDanceConverter.AppendRootOffset(rotationFrames[i], sourceFrames[sourceIndex]);
        }

        return output;
    }

    static string ToProjectRelative(string absolutePath)
    {
        string data = Application.dataPath;
        if (absolutePath.StartsWith(data))
        {
            return "Assets" + absolutePath.Substring(data.Length).Replace('\\', '/');
        }

        return absolutePath;
    }
}
