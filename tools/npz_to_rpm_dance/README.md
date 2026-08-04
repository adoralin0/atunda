# npz-json → RPM legacy dance pipeline

Converts ATUNDA / SAM-body4d exports (`format: npz-json`, e.g. `Assets/StreamingAssets/source.json`) into the **19-float-per-frame** JSON used by `AvatarAnimationPlayer` and `ReadyPlayerAvatar` (`Assets/Resources/Dances/*.json`).

## What it does

1. Parses `pred_keypoints_3d`, `keypoint_names`, `pred_cam_t`, `video_fps`.
2. Centers `pred_cam_t` (mean X/Y/Z subtracted) — stored for future use; dance playback uses bone rotations only.
3. Derives per-frame Euler values from keypoint directions (hips, spine, arms, legs) matching the RPM layout in `ReadyPlayerAvatar`.
4. Optionally resamples 60 fps → 30 fps for the default player.

## Unity (Editor)

Menu: **UPose → Convert npz-json to RPM Dance JSON**

- Default: `Assets/StreamingAssets/source.json` → `Assets/Resources/Dances/ETIGHI_reference.json`
- Or **UPose → Convert npz-json (pick files)...**

Play in game: `PlayDance("ETIGHI_1")` (no `.json` extension).

## Python (batch / CI)

```bash
cd tools/npz_to_rpm_dance
python convert.py -i ../../Assets/StreamingAssets/source.json -o ../../Assets/Resources/Dances/ETIGHI_reference.json --fps 30
```

Batch folder:

```bash
for f in /path/to/atunda/**/*.json; do
  python convert.py -i "$f" -o "../../Assets/Resources/Dances/$(basename $(dirname $f)).json" --fps 30
done
```

## Limitations

- This is **geometric IK**, not a direct decode of `body_pose_params` (SMPL axis-angle). Quality may differ from hand-tuned dances like `ETIGHI_reference.json`.
- Arm indices use `LookRotation` heuristics; RPM applies extra `Quaternion.Euler(0,0,±90)` on arms — tune axis flip or add calibration if poses look wrong.
- Default axis flip: `(1, -1, 1)` on keypoints (common Y-up vs capture space).

## Files

| File | Role |
|------|------|
| `Assets/Scripts/NpzJsonMotionParser.cs` | Parse npz-json |
| `Assets/Scripts/RpmDanceConverter.cs` | Keypoints → 19 floats |
| `Assets/Scripts/Editor/NpzToRpmDanceExporter.cs` | Unity export menu |
| `tools/npz_to_rpm_dance/convert.py` | Standalone batch converter |
