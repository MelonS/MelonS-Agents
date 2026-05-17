# Topic backlog — post-pick scale-up

> **Note — partially stale after 2026-05-17 niche pivot.**  This file was
> drafted when the candidate niches were `faceless-short` topic shorts
> (Hittites/Hydrogen).  The operator pick on 2026-05-17 was a **format
> pivot** to `music-video` mode (see [`decision-log.md` § Operator pick](decision-log.md#operator-pick--2026-05-17)),
> which does not consume topic prompts.  These entries remain as a
> backlog for any future narration-driven channel revival; they are
> NOT the production queue for the active niche.  See
> [`docs/roadmap.md`](../roadmap.md) "Next" for the actual queue.

Original intent: once the operator picks a niche from [`decision-log.md`](decision-log.md), this file was the next-5 queue.  Each entry is a ready-to-run `topic_prompt` argument for `agents/missions/faceless-short/run.sh`.

> **Fact-check before render.**  Many entries below were drafted by `llama3.2:3b` and the small-model drift is real — it invented "Hymenaei kingdom" (not biblical), confused the Cyrus Cylinder (Cyrus's, not Darius's) and a Silk-Road merchant with a biblical figure.  Entries marked **[CURATED]** are operator-or-Claude verified; entries marked **[AI-DRAFT]** are starter ideas that need a 30-second Wikipedia / Britannica check before running.  When in doubt, drop the entry and pick another.

## Niche A — Science

### 1. Dark Matter [AI-DRAFT — verified]

| | |
|---|---|
| Hook | Dark matter makes up about 85 % of the universe's mass-energy budget — and nobody has ever seen it. |
| Prompt | `Dark Matter — the invisible substance that dominates the cosmos` |
| B-roll likely | galaxies, telescope, simulation visuals, galactic rotation curves |
| Risk | Low — well-documented, broad stock-footage coverage. |

### 2. Quantum Entanglement [AI-DRAFT — verified]

| | |
|---|---|
| Hook | Two entangled particles can change each other's state instantly across any distance — Einstein called it "spooky action at a distance." |
| Prompt | `Quantum Entanglement — the spooky connection that breaks classical physics` |
| B-roll likely | atom animations, particle collider, abstract physics visualizations |
| Risk | Low — dense Pexels coverage for "quantum particles", "atoms", "physics simulation". |

### 3. Bioluminescent Bays [AI-DRAFT — verified]

| | |
|---|---|
| Hook | A handful of bays on Earth glow blue at night — bright enough to outline a fish swimming through. |
| Prompt | `Bioluminescent Bays — the oceans that glow at night and the microorganisms behind them` |
| B-roll likely | ocean at night, glowing water, plankton, kayak through bay |
| Risk | Low — visually rich, niche but globally appealing. |

### 4. Superconductivity [AI-DRAFT — verified]

| | |
|---|---|
| Hook | In 1911, a Dutch physicist cooled mercury to four degrees above absolute zero and watched its electrical resistance drop to literally zero. |
| Prompt | `Superconductivity — the 1911 discovery that broke our understanding of electricity` |
| B-roll likely | levitating magnets, MRI machines, lab equipment, abstract energy visualizations |
| Risk | Low — Heike Kamerlingh Onnes, 1911, real Nobel-prize-worthy history. |

### 5. Pleistocene Megafauna Extinction [CURATED — replaces AI draft]

| | |
|---|---|
| Hook | Around 11 000 years ago, two-thirds of large mammal genera vanished in less than a few millennia — and the cause is still contested. |
| Prompt | `Pleistocene Megafauna Extinction — what killed the mammoths, sabretooths, and giant ground sloths` |
| B-roll likely | mammoth illustrations, ice age tundra, cave paintings, fossil dig sites |
| Risk | Low — well-documented, ongoing debate (climate vs human-overkill hypothesis) makes for a strong middle section. |

---

## Niche B — Bible × history crossroads

> **Quality bar**: every entry here cites an archaeological artifact or document with a named discovery year.  The episodes are *not* theological commentary — they describe the physical evidence and the historical context, with the biblical reference as one data point among many.  Operator approved this framing in the active-goal phrasing.

### 1. Tel Dan Stele [CURATED]

| | |
|---|---|
| Hook | Until 1993, no extra-biblical source mentioned the House of David.  Then an Israeli archaeologist found a basalt fragment at Tel Dan inscribed with the phrase "House of David" in Old Aramaic. |
| Prompt | `Tel Dan Stele — the 1993 inscription that gave King David his first archaeological mention` |
| B-roll likely | ancient stone inscriptions, archaeological dig, Israeli landscape, basalt fragments |
| Risk | Low — Avraham Biran's 1993 discovery, dating well-attested. |

### 2. Sennacherib's Prism [CURATED]

| | |
|---|---|
| Hook | Both the Bible and an Assyrian clay prism describe Sennacherib's 701 BC siege of Jerusalem — but they tell almost the same story from opposite sides. |
| Prompt | `Sennacherib's Prism — when the Bible and an Assyrian king described the same siege` |
| B-roll likely | clay tablets, cuneiform, Mesopotamian ruins, ancient Jerusalem reconstructions |
| Risk | Low — Taylor Prism (1830 discovery, BM 91032) and Oriental Institute Prism are both real. |

### 3. Mesha Stele / Moabite Stone [CURATED]

| | |
|---|---|
| Hook | The 9th-century-BC king of Moab carved his side of the war with Israel onto a stone slab.  It was rediscovered in 1868, broken by villagers worried it would be confiscated, and reassembled at the Louvre. |
| Prompt | `Mesha Stele — the Moabite king's side of the war with Israel, carved in 840 BC` |
| B-roll likely | ancient stone tablets, Louvre, Middle Eastern desert, Hebrew/Phoenician script |
| Risk | Low — solid archaeological history, dramatic discovery narrative. |

### 4. Caiaphas Ossuary [CURATED]

| | |
|---|---|
| Hook | In 1990, construction workers in a Jerusalem park accidentally cracked open a 1st-century tomb.  Inside, a decorated bone box was inscribed with the name Yehosef bar Qayafa — the high priest from the trial of Jesus. |
| Prompt | `Caiaphas Ossuary — the 1990 Jerusalem discovery that named a New Testament high priest` |
| B-roll likely | ossuaries, ancient Hebrew inscriptions, Jerusalem aerial, archaeological lab |
| Risk | Medium — religious sensitivity; keep the framing strictly archaeological and avoid theological positioning. |

### 5. Pontius Pilate Inscription [AI-DRAFT — verified, refined hook]

| | |
|---|---|
| Hook | For centuries, Pilate's existence rested entirely on the Gospels and a few Roman historians.  In 1961, Italian archaeologists at Caesarea Maritima turned over a stone block and found his name carved into it. |
| Prompt | `Pontius Pilate Inscription — the 1961 stone block that confirmed a New Testament figure` |
| B-roll likely | Caesarea ruins, Roman inscriptions, Mediterranean coast, ancient Judea |
| Risk | Low — Antonio Frova's 1961 discovery, well-documented. |

---

## How to use this file

Pick a row, run:

```bash
./agents/missions/faceless-short/run.sh <id> "<prompt_from_table>"
./scripts/gen-upload-metadata.sh records/missions/<date>/faceless-<id>-<HHMMSS>
```

Then drop the rendered short, caption-verify thumbnail, and upload-metadata.md into `docs/pilots/screens/` + `docs/pilots/upload-metadata/` and append a row to `decision-log.md`.

Once a niche is picked + producing well, this file becomes the daily/weekly upload queue.  New ideas: write them in as **[CURATED]** with a fact-checked hook, or as **[AI-DRAFT]** if generated and not yet verified.
