def clamp($S; $rs; $re; $src_dur):
  ($S | map(select(.start <= ($rs + 0.5))) | last // $S[0] | .start) as $start0
  | ($S | map(select(.end >= ($re - 0.5))) | first // $S[-1] | .end) as $end0
  | ($end0 - $start0) as $d0
  | ([45, $src_dur - 0.1] | min) as $target
  | if $d0 >= 30 and $d0 <= 60 then
      {start: $start0, end: $end0}
    elif $d0 < 30 then
      ($S | sort_by(.start)) as $sorted
      | (($start0 + $end0) / 2) as $mid
      | ($mid - $target / 2) as $want_s
      | ($mid + $target / 2) as $want_e
      | ([$want_s, 0] | max) as $clip_s
      | ([$want_e, $src_dur] | min) as $clip_e
      | { start: ($sorted | map(.start) | map(select(. <= $clip_s)) | last // 0),
          end:   ($sorted | map(.end)   | map(select(. >= $clip_e)) | first // $src_dur) }
    else
      {start: $start0, end: ($start0 + 60)}
    end;

clamp($segs[0]; $rs; $re; $src_dur)
