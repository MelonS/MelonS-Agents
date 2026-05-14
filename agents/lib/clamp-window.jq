# Inputs:
#   $segs[0] : array of {start,end,text} segments
#   $rs, $re : model's chosen window (raw)
#   $src_dur : source media duration in seconds
# Output:
#   {start, end} clamped to [30, 60] seconds, snapped to segment edges,
#   never overshooting the source duration.

def snap_start_at($p):
  $segs[0] | map(.start) | map(select(. <= $p)) | last // 0;

def snap_end_at($p):
  $segs[0] | map(.end) | map(select(. >= $p)) | first // $src_dur;

# 1. Initial snap to model's window
( snap_start_at($rs + 0.5) ) as $s0
| ( snap_end_at($re - 0.5)  ) as $e0
| ($e0 - $s0) as $d0
# 2. Target midpoint and centered 45s window
| (($s0 + $e0) / 2) as $mid
| 45 as $target
| (
    if $d0 >= 30 and $d0 <= 60 then
      {start: $s0, end: $e0}
    elif $d0 < 30 then
      # expand: snap around mid, target 45s
      ( snap_start_at($mid - $target/2) ) as $ws
      | ( snap_end_at($mid + $target/2) ) as $we
      | {start: $ws, end: $we}
    else
      # shrink: keep $s0, cap at 60s
      {start: $s0, end: ($s0 + 60)}
    end
  ) as $w
# 3. Hard-cap: result must be ≤ 60s and ≤ source duration
| ($w.start) as $start
| ([$w.end, $start + 60, $src_dur] | min) as $end
# 4. Hard-floor: result must be ≥ 30s when source allows
| (if ($end - $start) < 30 and $src_dur >= 30 then
     ([$start + 30, $src_dur] | min) as $stretched
     | {start: $start, end: $stretched}
   else
     {start: $start, end: $end}
   end)
