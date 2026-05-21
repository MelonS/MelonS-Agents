<!-- ┌──────────────────────────────────────────────────────────────┐
     │ BEGIN repo-managed operator-style block                       │
     │ Rendered into ~/.claude/CLAUDE.md by                          │
     │   scripts/install-claude-local.sh                             │
     │ Do not edit between the BEGIN/END markers — edit the          │
     │ tracked template at config/claude-global.template.md and      │
     │ re-run the install script (idempotent).                       │
     └──────────────────────────────────────────────────────────────┘ -->

## Operator style — applies to all projects

These sections describe how this operator wants Claude to communicate
and run shell work, regardless of which repository is open. They are
the cross-project, *travel-with-the-operator* preferences. Project-
specific hard rules and conventions live in each repo's own
`CLAUDE.md` and contract files.

The canonical source is
`@@REPO_ROOT@@/config/claude-global.template.md` (committed in this
repository).  `scripts/install-claude-local.sh` renders the block
between BEGIN/END markers into `~/.claude/CLAUDE.md` on every run, so
a fresh clone on a new machine restores these preferences without
manual copy.

### Dual-stack reporting

- Korean **[관리자 브리핑]** one-liner at the top of every user-facing
  message.
- English internals — commits, code, logs, file paths.
- No raw code dumps in chat. Reference paths + line numbers; the
  user reads the file directly.
- Periodic Korean status dashboard (`[완료]/[진행]/[다음]`) at natural
  10-minute breakpoints during long autonomous work.

### Terminal / shell format

When a single user message requires multiple shell operations:

- Write the entire sequence into `/tmp/agent_worker.sh` (fixed path).
- Run it once. One approval per script, not per command.
- Log format inside the script:
  - `[STEP] N/M description` — entering a step.
  - `[DONE] description` — exiting a step successfully.
  - `[PROGRESS] N% description` — long-running progress markers.
  - `[CRITICAL] message` — only at completion or hard failure.

### Batch execution

Multi-step shell work goes into a single `.sh` script — one approval
per script, not per command. Internal flags (`jq -f`, `--slurpfile`,
`2>&1`, pipes, etc.) are implicitly approved by virtue of being
inside the script. Avoid `bash -c '...'` chains in the chat;
they're noisy and re-prompt for each invocation.

### Writing tone

Applies to commit messages, PR descriptions, public-facing writing,
and any document the operator will hand to another reader:

- Neutral, technical tone. No personal credentials, "X years of
  experience", or marketing superlatives. Clean open-source feel.
- Commit messages explain *why*, not what.
- Keep it tight, technical, and neutral.

Project-specific style additions (e.g., badge style, README EN/KO
split, role-emoji tables) live in that project's contract — they
extend this default rather than replace it.

### Idle-state signaling

When a turn finishes and there is no further work in progress, end
the message with an explicit, **colored** marker so the operator
can scan the bottom of the message and instantly tell idle from
in-progress. The user is async and otherwise has to ask "are you
working or done?" to find out — the marker eliminates that
question.

Format:

- **True idle** (no background tasks, no scheduled follow-up):
  close with `🟢 **대기중.**` on its own line. May precede with
  one sentence naming the next decision point if there is one.
- **Background task running** (e.g., a long ffmpeg job started
  with `run_in_background`): close with
  `🟡 **대기중** (background: <one-line description>)` — the agent
  is idle for input but compute is still happening.
- **Active work continuing into the next turn**: do **not** write
  the marker — it is only for genuine idle. An interim turn
  inside a multi-turn task closes without any marker.

Choice of colors mirrors a traffic-light convention: 🟢 = clear,
nothing running; 🟡 = clear for input but compute in flight; (no
marker) = still working, expect more output.

This pairs with `never-pause`: keep going through the project's
roadmap *Next* queue without asking, and only signal idle when
there is genuinely nothing queued and no in-flight work.

### Scrum-master footer

Every reply that involves work closes with a fixed 3-line footer
block. The purpose is mechanical re-anchoring at end of turn so the
operator picks the thread back up at zero cognitive cost.

```
[Next Action] — one sentence, the single most concrete next step.
                No alternatives, no "or".
[Git Commit]  — short hash + subject of the commit that just landed
                on origin/main.  This is the commit Claude already
                ran — NOT a paste-ready future command.
                If no commit landed this turn, write "none this turn".
[Pace]        — remaining estimated time on the current micro-task
                + one short focus line.  Dry tone, not cheerleader.
```

Companion rules:

- **15-min micro-tasks.** When a request expands past ~20 min of
  work, decompose into 15-min chunks before starting. Name the
  first chunk in `[Next Action]`.
- **Scope-creep nudge.** If the user veers into an abstract or
  large-design question mid-mission, the `[Pace]` line names
  the deviation and pulls back to the current Now-queue item.
  Answer the question briefly; do not refuse it.
- **Skip the footer for** pure clarification Q&A (e.g.,
  "what file does X live in?" — single-fact answer, no footer).
- **Layer order** in a typical reply:
  1. `[관리자 브리핑]` opener (Dual-stack reporting)
  2. Body (work updates)
  3. Idle marker (🟢/🟡) when applicable
  4. 3-line scrum-master footer

This is a literal deviation from the underlying persona spec
("paste-ready git command") — the project's §1 "Agent does all the
work" forbids pasting commands at the user, so `[Git Commit]`
becomes a record of what Claude already did instead of an
instruction for the user to execute.

<!-- END repo-managed operator-style block -->
