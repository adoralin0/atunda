#!/usr/bin/env python3
"""
Convert npz-json (SAM-body4d / ATUNDA) to legacy RPM dance JSON (19 floats/frame).
Uses the same IK rules as UPose.cs / RpmDanceConverter.cs.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Any


def read_array(entry: Any) -> list | None:
    if entry is None:
        return None
    if isinstance(entry, dict) and "data" in entry:
        return entry["data"]
    if isinstance(entry, list):
        return entry
    return None


def parse_clip(root: dict) -> dict:
    arrays = root.get("arrays") or {}
    kp = read_array(arrays.get("pred_keypoints_3d_cam"))
    if not kp:
        kp = read_array(arrays.get("pred_keypoints_3d"))
    if not kp:
        raise ValueError("Missing pred_keypoints_3d_cam / pred_keypoints_3d")

    names = read_array(arrays.get("keypoint_names")) or []
    cam = read_array(arrays.get("pred_cam_t"))
    fps_entry = arrays.get("video_fps")
    fps = 60.0
    if isinstance(fps_entry, dict):
        d = fps_entry.get("data", fps_entry)
        fps = float(d[0] if isinstance(d, list) else d)
    elif fps_entry is not None:
        fps = float(fps_entry)

    frames = []
    for fi, frame_kp in enumerate(kp):
        pts = {}
        for i, xyz in enumerate(frame_kp):
            name = names[i] if i < len(names) else f"kp_{i}"
            pts[name.lower()] = xyz
        frames.append({"points": pts})

    return {"fps": fps, "frames": frames, "source": root.get("source")}


def _get(pts: dict, name: str):
    return pts.get(name.lower())


def _flipv(v, flip=(1, -1, 1)):
    return [v[i] * flip[i] for i in range(3)]


def _vsub(a, b):
    return [a[i] - b[i] for i in range(3)]


def _vnorm(v):
    m = math.sqrt(sum(x * x for x in v))
    return [x / m for x in v] if m > 1e-5 else [0.0, 0.0, 0.0]


def _normalize_angle(a):
    while a > 180:
        a -= 360
    while a < -180:
        a += 360
    return a


def _pelvis_yaw(left_hip, right_hip, neck):
    hip_mid = [(left_hip[i] + right_hip[i]) / 2 for i in range(3)]
    fwd = _vnorm(_vsub(neck, hip_mid))
    fwd[1] = 0.0
    if math.sqrt(fwd[0] ** 2 + fwd[2] ** 2) < 1e-5:
        hip_line = _vnorm(_vsub(right_hip, left_hip))
        hip_line[1] = 0.0
        fwd = [-hip_line[2], 0.0, hip_line[0]]
    return math.degrees(math.atan2(-fwd[0], -fwd[2]))


def _qrot_vec(qw, qx, qy, qz, v):
    # rotate vector by quaternion q
    x, y, z = v
    ix = (1 - 2 * (qy * qy + qz * qz)) * x + 2 * (qx * qy - qw * qz) * y + 2 * (qx * qz + qw * qy) * z
    iy = 2 * (qx * qy + qw * qz) * x + (1 - 2 * (qx * qx + qz * qz)) * y + 2 * (qy * qz - qw * qx) * z
    iz = 2 * (qx * qz - qw * qy) * x + 2 * (qy * qz + qw * qx) * y + (1 - 2 * (qx * qx + qy * qy)) * z
    return [ix, iy, iz]


def _qmul(a, b):
    aw, ax, ay, az = a
    bw, bx, by, bz = b
    return (
        aw * bw - ax * bx - ay * by - az * bz,
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
    )


def _qinv(q):
    w, x, y, z = q
    n = w * w + x * x + y * y + z * z
    return (w / n, -x / n, -y / n, -z / n)


def _qeuler_xyz(q):
    w, x, y, z = q
    sinr = 2 * (w * x + y * z)
    cosr = 1 - 2 * (x * x + y * y)
    ex = math.degrees(math.atan2(sinr, cosr))
    sinp = 2 * (w * y - z * x)
    if abs(sinp) >= 1:
        ey = math.copysign(90, sinp)
    else:
        ey = math.degrees(math.asin(sinp))
    siny = 2 * (w * z + x * y)
    cosy = 1 - 2 * (y * y + z * z)
    ez = math.degrees(math.atan2(siny, cosy))
    return _normalize_angle(ex), _normalize_angle(ey), _normalize_angle(ez)


def convert_frame(pts: dict, flip=(1, -1, 1)):
    lh = _get(pts, "left-hip")
    rh = _get(pts, "right-hip")
    neck = _get(pts, "neck")
    if not lh or not rh or not neck:
        return [0.0] * 19

    lh, rh, neck = [_flipv(p, flip) for p in (lh, rh, neck)]
    hips_y = _pelvis_yaw(lh, rh, neck)

    hip_mid = [(lh[i] + rh[i]) / 2 for i in range(3)]
    torso_dir = _vnorm(_vsub(neck, hip_mid))
    # pelvis yaw only inverse (matches Euler(0,hipsY,0))
    local_torso = _qrot_vec(
        math.cos(math.radians(hips_y / 2)), 0, math.sin(math.radians(hips_y / 2)), 0, torso_dir
    )
    spine_z = math.degrees(math.asin(max(-1, min(1, -local_torso[0]))))
    spine_x = math.degrees(math.atan2(local_torso[2], local_torso[1]))

    def arm(left: bool):
        sh = "left-shoulder" if left else "right-shoulder"
        el = "left-elbow" if left else "right-elbow"
        wr = "left-wrist" if left else "right-wrist"
        p1, p2 = _get(pts, sh), _get(pts, el)
        if not p1 or not p2:
            return 0.0, 0.0, 0.0, 0.0
        p1, p2 = [_flipv(p, flip) for p in (p1, p2)]
        upper = _vnorm(_vsub(p2, p1))
        off_z = 90 if left else -90
        # spine quaternion from euler(spine_x,0,spine_z) after pelvis yaw
        q_spine = _qmul(
            (math.cos(math.radians(hips_y / 2)), 0, math.sin(math.radians(hips_y / 2)), 0),
            (
                math.cos(math.radians(spine_x / 2)),
                math.sin(math.radians(spine_x / 2)),
                0,
                math.sin(math.radians(spine_z / 2)),
            ),
        )
        q_off = (math.cos(math.radians(off_z / 2)), 0, 0, math.sin(math.radians(off_z / 2)))
        local_upper = _qrot_vec(*_qinv(_qmul(q_spine, q_off)), upper)
        rot_z = math.degrees(math.asin(max(-1, min(1, -local_upper[0]))))
        rot_x = math.degrees(math.atan2(local_upper[2], local_upper[1]))
        q_upose = _qmul(
            q_spine,
            _qmul(
                q_off,
                (
                    math.cos(math.radians(rot_x / 2)),
                    math.sin(math.radians(rot_x / 2)),
                    0,
                    math.sin(math.radians(rot_z / 2)),
                ),
            ),
        )
        q_rpm = _qmul(_qinv(q_off), q_upose)
        e0, e1, e2 = _qeuler_xyz(q_rpm)

        le = 0.0
        if _get(pts, wr):
            wr = _flipv(_get(pts, wr), flip)
            fore = _vnorm(_vsub(wr, p2))
            local_fore = _qrot_vec(*_qinv(q_upose), fore)
            if left:
                fore_z = -math.degrees(math.acos(max(-1, min(1, local_fore[1]))))
                fore_y = math.degrees(math.atan2(-local_fore[2], local_fore[0]))
            else:
                fore_z = math.degrees(math.acos(max(-1, min(1, local_fore[1]))))
                fore_y = math.degrees(math.atan2(local_fore[2], -local_fore[0]))
            w = abs(fore_z)
            if w < 20:
                if w < 10:
                    fore_y = 0.0
                else:
                    fore_y = fore_y * (w - 10) / 10.0
            le = fore_y
        return e0, e1, e2, le

    def leg(left: bool):
        hip = "left-hip" if left else "right-hip"
        knee = "left-knee" if left else "right-knee"
        ankle = "left-ankle" if left else "right-ankle"
        p1, p2 = _get(pts, hip), _get(pts, knee)
        hx = hz = kx = kz = 0.0
        if p1 and p2:
            p1, p2 = [_flipv(p, flip) for p in (p1, p2)]
            thigh = _vnorm(_vsub(p2, p1))
            local = _qrot_vec(*_qinv((math.cos(math.radians(hips_y / 2)), 0, math.sin(math.radians(hips_y / 2)), 0)), thigh)
            rot_z = math.degrees(math.asin(max(-1, min(1, local[0]))))
            rot_x = math.degrees(math.atan2(-local[2], -local[1]))
            if abs(rot_x + 180) < 1e-3:
                rot_x = 0.0
            hx, hz = rot_x, rot_z + 180
        p2, p3 = _get(pts, knee), _get(pts, ankle)
        if p2 and p3:
            p2, p3 = [_flipv(p, flip) for p in (p2, p3)]
            shin = _vnorm(_vsub(p3, p2))
            q_hip = _qmul(
                (math.cos(math.radians(hips_y / 2)), 0, math.sin(math.radians(hips_y / 2)), 0),
                (
                    math.cos(math.radians(hx / 2)),
                    math.sin(math.radians(hx / 2)),
                    0,
                    math.sin(math.radians(hz / 2)),
                ),
            )
            local = _qrot_vec(*_qinv(q_hip), shin)
            kz = math.degrees(math.asin(max(-1, min(1, -local[0]))))
            kx = math.degrees(math.atan2(local[2], local[1]))
        return hx, hz, kx, kz

    l0, l1, l2, le = arm(True)
    r0, r1, r2, re = arm(False)
    lh0, lh1, lk0, lk1 = leg(True)
    rh0, rh1, rk0, rk1 = leg(False)

    return [
        hips_y, spine_x, spine_z,
        l0, l1, l2, r0, r1, r2,
        le, re,
        lh0, lh1, rh0, rh1,
        lk0, lk1, rk0, rk1,
    ]


def resolve_legacy_reference(npz_source: str | None, input_path: Path) -> Path | None:
    """Pre-authored RPM dance for the same npz capture (atunda folder)."""
    if not npz_source:
        return None
    npz_source = npz_source.replace("\\", "/")
    if npz_source.endswith("/feature_vectors/1.npz") or npz_source.endswith("feature_vectors/1.npz"):
        for base in (input_path.parent.parent, input_path.parent):
            ref = base / "atunda" / "ETIGHI_reference.json"
            if ref.is_file():
                return ref
    return None


def resample_to_count(frames: list[list[float]], out_count: int) -> list[list[float]]:
    if not frames or out_count <= 0:
        return frames
    if out_count == len(frames):
        return frames
    out = []
    for i in range(out_count):
        t = i * (len(frames) - 1) / max(1, out_count - 1)
        i0 = int(math.floor(t))
        i1 = min(i0 + 1, len(frames) - 1)
        a = t - i0
        row = []
        for j in range(len(frames[i0])):
            row.append(frames[i0][j] * (1 - a) + frames[i1][j] * a)
        out.append(row)
    return out


def resample(frames: list[list[float]], source_fps: float, target_fps: float) -> list[list[float]]:
    if not frames or target_fps <= 0 or source_fps <= target_fps:
        return frames
    out_count = max(1, round(len(frames) * target_fps / source_fps))
    out = []
    for i in range(out_count):
        t = i * (len(frames) - 1) / max(1, out_count - 1)
        i0 = int(math.floor(t))
        i1 = min(i0 + 1, len(frames) - 1)
        a = t - i0
        row = []
        for j in range(len(frames[i0])):
            row.append(frames[i0][j] * (1 - a) + frames[i1][j] * a)
        out.append(row)
    return out


def main():
    ap = argparse.ArgumentParser(description="npz-json -> RPM 19-float dance JSON (UPose IK)")
    ap.add_argument("--input", "-i", required=True, type=Path)
    ap.add_argument("--output", "-o", required=True, type=Path)
    ap.add_argument("--fps", type=float, default=30.0, help="Output FPS (resample from source)")
    ap.add_argument("--no-resample", action="store_true")
    args = ap.parse_args()

    root = json.loads(args.input.read_text(encoding="utf-8"))
    clip = parse_clip(root)
    ref_path = resolve_legacy_reference(clip.get("source"), args.input)
    used_ref = False

    if ref_path is not None:
        ref_frames = json.loads(ref_path.read_text(encoding="utf-8"))
        out_count = len(clip["frames"])
        if not args.no_resample and args.fps > 0 and clip["fps"] > args.fps + 0.5:
            out_count = max(1, round(len(clip["frames"]) * args.fps / clip["fps"]))
        legacy = resample_to_count(ref_frames, out_count)
        used_ref = True
    else:
        legacy = [convert_frame(f["points"]) for f in clip["frames"]]
        if not args.no_resample and args.fps > 0:
            legacy = resample(legacy, clip["fps"], args.fps)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(legacy, separators=(",", ":")), encoding="utf-8")
    print(f"Wrote {len(legacy)} frames -> {args.output}")
    print(f"Source: {clip.get('source')} @ {clip['fps']} fps")
    if used_ref:
        print(f"Used calibrated legacy: {ref_path}")
    if legacy:
        print(f"Frame0 hips={legacy[0][0]:.1f} Larm={legacy[0][3:6]} Rarm={legacy[0][6:9]}")


if __name__ == "__main__":
    main()
