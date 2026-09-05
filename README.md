# System-First Development Plan — NPC Reality Deduction Game

> เวอร์ชันเอกสาร: 1.0
> วันที่: 2026-09-04
> เป้าหมาย: เปลี่ยน technical prototype ที่พิสูจน์ระบบ NPC / Event / Memory / Suspicion / Reality Anomalies / Conspiracy แล้ว ให้เป็น First Fun Playtest ผ่าน 2D Top-Down Systemic Mystery + Visual Novel Hybrid

---

## สถานะการพัฒนาปัจจุบัน

Roadmap Phase 0–15 และ Post-MVP เขียนโค้ดครบแล้วทั้งหมด แต่การไล่ตรวจเมื่อ 2026-09-04 พบว่า **ระบบจำนวนหนึ่งเสร็จแล้วแต่ไม่เคยถูกต่อเข้าเกมที่เล่นได้จริง** — `SecretPlanRepository`, `SecretGoalSource`, `SecretBehaviorSystem`, `NeedGoalSource` ถูกสร้างเฉพาะใน test ส่วน `RealityAnomalySystem` กับ `ConspiracySystem` ถูกสร้างแต่ไม่มีอะไรกระตุ้น และตารางงานของ NPC ทุกคนเป็น `Idle` ตลอด 24 ชั่วโมง

บทเรียนคือ **“เขียนเสร็จ” ไม่เท่ากับ “ทำงานในเกม”** — เกณฑ์วัดที่ใช้ได้จริงคือมันผลิตเหตุการณ์ที่ผู้เล่นเห็นได้หรือไม่ สถานะด้านล่างจึงจัดกลุ่มตามสิ่งที่ทำงานจริงในเกม ไม่ใช่ตามลำดับ phase:

### แกนจำลองและความสามารถในการทำซ้ำ

ส่วนที่พิสูจน์แล้วว่าทำงานถูกต้องและรันซ้ำได้ด้วย seed เดียวกัน

- **Core Simulation & Determinism:** Pure C# .NET 8 simulation, deterministic clock, PCG32 RNG implementation (เชื่อมเข้ากับ `CaseGenerator` แล้ว), strongly typed IDs, Entity/Location topology, immutable WorldEvent stream, atomic MoveEntity, deterministic JSONL event logger, 283/283 xUnit automated tests.
- **Perception, Memory & Suspicion:** Visual/Audio observation, Episodic/Social MemoryStore, RootEventId rumor lineage, confidence decay, 11 data-driven SuspicionRules, EvidenceContribution, SuspicionVector 6 มิติ (Criminality, Secrecy, RoleDeviation, MetaBehavior, ImpossibleBehavior, Deception).
- **NPC Brain & Autonomous Feedback Loop:** Daily Schedule, Needs, Role permissions, deterministic Utility-based NPC Brain, Secret Plans (theft/secret meeting/night owl), belief-driven goals (observe/follow/ask/share/avoid/confront), rule-based behavior pattern detectors.
- **Sim Clock ตรงกับนาฬิกาของเกม:** เดิม `SimClock.TimeOfDay` คิดว่า 1 tick = ¼ วินาที (240 ticks = 1 นาที) ขณะที่ HUD คิดว่า 1 tick = 1 นาที ตารางจึงค้างที่ 00:00 ทั้งคืน; เพิ่ม `startOfDay` กับ `ticksPerMinute` โดย session ส่ง 23:00 กับ 1 เมื่อมี `SessionTruth`.
- **Deterministic Case Generation:** เพิ่ม `Game.Sim/Cases/` ประกอบด้วย `SessionTruth`, `CaseGenerator`, `CaseGenerationOptions`, `SecretAssignment` และ `AnomalyBeat`; seed เดียวกันได้ case เดิมเสมอ ส่วน seed ต่างกันเปลี่ยน Hidden Player, archetype, Incident Culprit, secrets และ anomaly schedule โดย content พินค่าใดไว้ก็ได้ (first playable case พินเฉพาะ `incidentCulprit` เพราะบทจบอ้างถึงโดยตรง); `BasementScenarioOptions.Truth` เป็น optional จึงไม่กระทบ fingerprint ของ 4 regression scenario เดิม.
- **Save-Load Session Snapshot System:** ถ่ายทอดและกู้คืนสถานะโลกจำลองทั้งหมดผ่าน Pure JSON พร้อมฟังก์ชัน QuickSave (`F6`) / QuickLoad (`F7`) ใน Godot Client และ SimRunner CLI flags (`--save-snapshot` / `--load-snapshot`) โดยรับประกันความถูกต้อง 100% Deterministic Parity.
- **Multi-Scenario Headless Stress Testing Suite:** ชุดทดสอบ 4 Scenarios บน SimRunner (`basement`, `rumor-cascade`, `deceptive-alibi`, `reality-breach`) พร้อมระบบคำนวณ SHA-256 Event Fingerprint และ JSONL Traces.
- **Reality Anomalies & Meta-Suspicion System (The Core Concept):** ตรวจจับความผิดปกติของมิติเวลา เช่น การโหลดเซฟ (SaveReload Déjà Vu) และการเคลื่อนที่ฉับพลัน (The Blink Fast Travel) เพื่อกระตุ้นค่าความสงสัยในมิติ `ImpossibleBehavior` และ `MetaBehavior` ต่อ The Player.
- **NPC Collective Conspiracy & Climax Accusation System:** ระบบรวมกลุ่มพันธมิตร NPC (`AccusationCoalition`), การสะสมหลักฐานจนเกิดมติเอกฉันท์ (`ConsensusReached`), การเรียกประชุมชี้ตัวใน Lobby, และทางเลือกของผู้เล่นสู่ฉากจบ (`Z` Confess, `X` Deny, `C` Flee).

### โลกที่มีอะไรให้สังเกต

ระบบเหล่านี้เขียนเสร็จมานานแล้วแต่ **ไม่เคยถูกต่อเข้าเกมจริง** — ก่อนหน้านี้คืนหนึ่งผลิตเหตุการณ์ 13 ชิ้นโดย 60% ของการตัดสินใจคือ `Idle` ตอนนี้ ~158 ชิ้นและไม่มี `Idle` เลย

- **Observable Normality (§0.10.1):** เดิม NPC ทุกคนมีตาราง `Idle` 24 ชั่วโมง จึงไม่มี “ปกติ” ให้เบี่ยงเบน; เพิ่ม `HotelNightRoutines` กำหนดตารางกะกลางคืนจริงตามบทบาท (พนักงานต้อนรับ/แม่บ้าน/รปภ./เชฟ/ผู้จัดการ/แขก) พร้อมสิทธิ์เข้าห้องตามบทบาท และ `RoleDutySystem` ที่ยิง `RoleDutyMissed` — ตัวผลิตที่ขาดหายทำให้ `RoleNeglect` ไม่เคยทำงาน. วัดผลต่อหนึ่งคืนเต็ม: decisions ที่เป็น Idle จาก 60% เหลือ 0%, `RoleDutyMissed` จาก 0 เป็น 9 และแฟ้มคดีมีเบาะแสจริง 18 ชิ้น.
- **False Positive เกิดได้จริงแล้ว (§0.5):** ต่อ `SessionTruth.Secrets` เข้า `SecretPlanRepository` ผ่าน `HotelSecretStaging` — seed กำหนดว่าใครมีความลับแบบไหน ส่วนฉากโรงแรมกำหนดว่ามันเกิดที่ไหนเมื่อไร (แยกกันเพื่อให้ `SessionTruth` ยังใช้กับฉากอื่นได้); `SecretGoalSource` เข้า brain และ `SecretBehaviorSystem` เป็น observer. ผลคือ `Theft` กับ `NightActivity` **ยิงได้เป็นครั้งแรก** แปลว่าตอนนี้ NPC ธรรมดาที่มีความลับดูผิดปกติได้เท่า Player จริง — กฎ “แปลก = Player” ใช้ไม่ได้อีกต่อไป.
- **ทุกคนตอบสนองต่อความสงสัยได้ (§0.8):** เดิมมีเพียง Anna กับ Bob ที่มี `SuspicionBehaviorProfile` และ Bob ไม่มีคนให้เล่าเลย; เพิ่ม `HotelSocialGraph` กำหนดว่าแต่ละบทบาทไว้ใจใครและหลบไปที่ไหน โดย test บังคับว่ากราฟต้อง strongly connected — จับได้ว่าร่างแรกของผมทำให้ฝั่งผู้บริหารเป็นวงปิด ข่าวเข้าได้แต่ออกไม่ได้. คืนหนึ่งที่ seed 481516 เกิดสายความสงสัยอิสระ 3 สาย จากกฎคนละแบบ: `witnessed_theft`, `witnessed_night_activity`, `detected_role_neglect`.
- **Reality Anomalies เกิดขึ้นจริง (§0.6):** ต่อ `truth.AnomalySchedule` เข้าลูป simulation — เดิม `CaseGenerator` สร้างตารางไว้แล้วไม่มีใครอ่าน `RealityAnomalySystem` จึงไม่เคยยิง event ในเกมจริงเลย; เพิ่ม `TriggerDialogueResetAnomaly` ที่ขาดหายตาม enum. anomaly เกิดกับ Hidden Player เท่านั้น — ความไม่แน่นอนมาจากว่ามีใครอยู่ตรงนั้นไหมและข่าวที่ได้ยินต่อน่าเชื่อแค่ไหน ไม่ใช่จาก anomaly ปลอม; ถ้า host อยู่ในห้องขณะมันเกิด จะมี alert ขัดขึ้นมา.
- **Needs ทำงานแล้ว (§14):** `NeedGoalSource` เข้า brain และ `HotelNeeds` ตั้งอัตราให้ความหิวมาถึงราว 02:30 และความล้าราว 04:00 ภายในกะเดียว — เพิ่ม**เหตุผลบริสุทธิ์อีกข้อที่คนจะออกจากที่ประจำ** ตอนตีสี่.
- **Tuning Pass:** เกณฑ์ pattern ถูกเขียนไว้สมัย 1 tick = ¼ วินาที — `LootSweep` ต้องการ 10 วัตถุจากที่มีทั้งหมด 11 ชิ้น และ `RoleNeglect` มีหน้าต่าง 60 ชั่วโมง (ยาวกว่ากะที่มันอธิบาย 10 เท่า); เพิ่ม `HotelNightRoutines.PatternPolicy()` ที่ขนาดสมกับโรงแรมนี้.
- **แก้ anomaly นับคะแนนสองเด้ง:** `AnomalyTags` ชุดเดียวถือทั้ง `Pattern` และ `Suspicious` ทำให้กฎ anomaly ทั้งสองข้อ (ซึ่งเป็นทางเลือกกัน) ยิงพร้อมกัน; แยก tag ตามชนิด anomaly และเพิ่ม `MaxBeliefWeight` จำกัดว่าความสงสัยดึง utility ได้แค่ไหน — เดิมคะแนนไม่มีเพดาน ทำให้ทั้งโรงแรมทิ้งงานไปไล่ตามคนเดียวทั้งคืน. fingerprint ของ `reality-breach` เปลี่ยนจากการแยก tag นี้โดยตั้งใจ.

### ลูปการเล่น — สิ่งที่ทำให้ผู้เล่นต้องตัดสินใจ

เดิมการสืบสวนไม่มีต้นทุน กลยุทธ์ที่ดีที่สุดคือ “ทำทุกอย่างให้ครบ” ซึ่งไม่ใช่การตัดสินใจ

- **เล่นคืนแรกจริง (ก่อน Milestone 6):** เพิ่ม `--night-report` แล้วเดินครบคืนบน 15 seed
ผลคือ **สองในสามเสาหลักไม่เคยทำงานในคืนปกติ** — exposure สูงสุด 0–14 จากเกณฑ์ 15 และคดี
ที่โรงแรมมีต่อผู้เล่นสูงสุด 0–24 จากเกณฑ์ 90 ส่วนคืน "You Were The Player" กลับตรงข้าม:
anomaly ครั้งเดียวดัน exposure ไป 60 และ coalition ไป 204–330 ทำให้ผู้เล่นถูกล้อมจับที่
20–38% ของกะ **ฉากจบที่เพิ่งทำเสร็จจึงไปไม่ถึงเลย** รายละเอียดและข้อค้นพบที่เหลืออยู่ใน
`playtest/first-night-findings.md` — ยังไม่แก้ตัวเลข เพราะเป็นการตัดสินใจด้านบาลานซ์
ที่ควรตกลงก่อนลงมือ.
- **ฉากจบ "You Were The Player" (Milestone 5):** เปิด `AllowHostAsHiddenPlayer` ในเกมจริง — ราวหนึ่งในหกคืน ตัวละครที่มนุษย์ขับคือคนที่ถูกบงการเสียเอง; หน้าสรุปคดีเพิ่มตัวเลือก “เอ่ยชื่อตัวเอง” และฉากจบแยกเป็นสามทางตาม `EndingKind` (`CorrectAccusation` / `FalseAccusation` / `YouWereThePlayer`). เมื่อ host เป็น hidden player **Player AI จะไม่ขับเขา** — นอกจากกันชนกับมนุษย์แล้ว ยังเป็นหัวใจของฉากนี้: พฤติกรรมแบบ Player ที่ทั้งโรงแรมตอบสนอง ต้องเป็นของมนุษย์เอง และ anomaly ก็เกิดกับ host ทำให้ Exposure กับ Closing Net มาบรรจบที่ผู้เล่นเอง โดยไม่ต้องสคริปต์เพิ่ม.
- **Exposure — ผู้เล่นก็ถูกโลกสังเกต (§0.7 Deviation):** อ่าน suspicion pipeline กลับด้านโดยใช้ Human Host เป็น subject ผ่าน `ExposureReport` แล้วแสดงเป็นภาษาคน 4 ระดับ (`ยังไม่มีใครสนใจ → มีคนสังเกต → ถูกจับตา → จนมุม`) บน HUD, ข้างนาฬิกา, หน้า `คนอื่นมองคุณอย่างไร` ในแฟ้มคดี และ alert เมื่อระดับขยับขึ้น; มีราคาจริงคือคนที่จับได้จะตอบอย่างระมัดระวัง (Watched) และเลิกเล่าเรื่องคนอื่นให้ฟัง (Cornered) โดยคิดเป็นรายคน ไม่ใช่ค่ารวม จึงไม่ปิดทางเล่นทั้งหมด.
- **Contradiction — จับโกหกได้:** การถามตารางงานทำให้ NPC ให้ `AlibiClaim` ที่ตรวจสอบได้ โดยคำตอบมาจากความทรงจำของเขาเอง และจะ**เลี่ยงพูดความจริงเมื่อความจริงคือห้องหวงห้าม** โดยกล่าวถึงห้องธรรมดาข้างเคียงแทน; `ContradictionFinder` เทียบคำให้การกับเบาะแสของผู้เล่น และการแย้งมีสองทาง: ถ้าคำให้การเท็จจะ `Cracked` และยอมบอกสิ่งที่เก็บไว้ แต่ถ้าคำให้การจริงจะ `Backfired` และ**เพิ่ม Exposure ของผู้กล่าวหาเอง** ทำให้เบาะแสที่เห็นเองกับที่ได้ยินต่อกันมามีน้ำหนักต่างกันจริง.
- **The Closing Net — นาฬิกาที่มีรูปทรง:** ต่อ `ConspiracySystem` เข้า 2D client แล้ว — NPC รวมกลุ่มกันต่อ Human Host ตามหลักฐานที่เห็นจริง โดยประกาศเตือนทุกขั้น (`Forming → ConsensusReached → Confronting`) และมีตัวนับถอยหลัง 45 นาทีในเกมก่อนลงมือ เพื่อให้ผู้เล่น**เห็น**ตาข่ายกำลังปิด และยังเลือกกล่าวหาก่อนได้; ถ้าปล่อยจนตาข่ายปิด จะเข้าฉากชี้ตัวแทน โดยเหลือทางเลือก `สารภาพ / ปฏิเสธ / หนี`. เกณฑ์ติดอาวุธใช้ `CombinedSuspicionScore >= 90` ไม่ได้ผูกกับ `ExposureLevel` เพราะสองมาตรวัดนี้ตอบคนละคำถามกัน.
- **Pacing:** ขยายกะจาก 3 นาทีจริงเป็น 9 นาที (`SecondsPerTick` 0.5 → 1.5) เพราะลูป `สืบ → ซ่อนตัว → สืบ` ต้องการหลายรอบจึงจะอ่านเป็นจังหวะได้.
- **Player Agency & Human Interaction Loop:** ระบบเข้าสิงตัวละคร (`P`), สั่งเดินนำทาง (`1-8`), สนทนาถามไถ่และแลกเปลี่ยนข่าวลือ (`T`), สมุดบันทึกประวัติความจำและข้อสงสัย Player Journal (`J`).
- **Dialogue & Clue Inquiry Expansion:** ระบบสอบถามเบาะแสเฉพาะวัตถุ (`InquireAboutObject`) และการนำหลักฐานไปเผชิญหน้า (`ConfrontEvidence`) ผ่านปุ่ม **`Y`**.
- **Case File Is About Other People:** `GetJournal` เคยใส่ความทรงจำทุกชิ้นรวมถึงการกระทำของผู้เล่นเอง ทำให้แฟ้มคดีเต็มไปด้วย “จอร์จแตะหรือตรวจบางอย่าง”, เสนอสิ่งเหล่านี้เป็นหลักฐานไปยันหน้าคนอื่น และ—เพราะ self-memory มี confidence เต็ม—กลายเป็น “เบาะแสที่เด่นที่สุด” ที่ถูกอ้างในฉากจบ.
- **Inspect ≠ Tamper:** แก้ `ObjectActionHandler.Inspect` ที่ติด tag `Suspicious` ให้การสำรวจธรรมดา ทำให้แค่เปิดอ่านสมุดทะเบียนแขกก็ถูกมองเท่ากับการงัดตู้นิรภัย (field ชื่อ `IsSuspiciousToTamper`); fingerprint ของ `deceptive-alibi` เปลี่ยนจากการแก้นี้โดยตั้งใจ ส่วน basement/rumor-cascade/reality-breach เท่าเดิม.

### การนำเสนอและเนื้อหา

- **Presentation Direction:** เลือกเป้าหมายเป็น **Stylized 2D Top-Down Hotel Simulation + Visual Novel Dialogue + Detective Journal** เพื่อให้ schedule, movement, rumor และ suspicion อ่านง่ายและเหมาะกับผู้พัฒนา Godot มือใหม่.
- **2D Graybox Foundation:** เพิ่ม production scene `Scenes2D/Main2D.tscn`, แผนที่โรงแรม 8 ห้อง, character tokens 6 คน, click-to-move, simulation clock, event feedback และ 2D movement acknowledgement โดยล็อก George เป็น Human Host; ตั้งฉากนี้เป็น main scene แล้ว.
- **Data-driven First Case:** เพิ่ม `characters.json` และ `first-playable-case.json` พร้อม schema validation โดยล็อกเคสแรกเป็น George/Host, Clara (`charlie`)/Hidden Player และ George/Incident Culprit.
- **Hotel Content & Encounters Expansion:** แผนที่โรงแรมขยายเป็น 8 ห้อง (*Lobby, Hallway, Kitchen, Room 201, Basement, Garden, Security Room, Manager Office*), ระบบวัตถุ Interactive Objects (เซฟ, สมุดทะเบียนแขก, สมุดบัญชีลับ, ตู้ไฟ, กุญแจ) พร้อมปุ่มสำรวจและงัดแงะ (`O`).
- **Investigation UX:** ลดแผงคำสั่งหลักเหลือ `สำรวจห้อง`, `เปิดแฟ้มคดี` และ `กล่าวหาผู้ต้องสงสัย`; คลิกตัวละครในห้องเพื่อเปิดคำสั่ง `คุย/ติดตาม/ถามด้วยเบาะแส` ตามบริบท แยกบทสนทนาออกจากบันทึกของจอร์จ และแบ่งแฟ้มคดีเป็นหน้าละ 2 เบาะแสแทน scrollbar ยาว.
- **Guided Playtest UX:** เพิ่มหน้าแรก, วิธีเล่น, เมนูพักเกมและตั้งค่าใน flow เดียว; ภาษาเปลี่ยนเฉพาะใน Settings เพื่อลดหน้าซ้ำซ้อน, onboarding สรุปลูป `เดิน → สังเกต → เปรียบเทียบ → ตัดสินใจ`, ตัวละครใช้จุดขนาดเล็กและแสดงชื่อเฉพาะคนที่เลือกเพื่อไม่บังแผนที่.
- **Playable Night Shift:** ขยาย prototype เป็นกะกลางคืน 360 นาทีในเกม (ประมาณ 3 นาทีจริง), เวลาและ AI เดินต่อระหว่างสนทนา, เพิ่ม deterministic shift beats/routines, เหตุการณ์แทรก, Insight View, final deduction, ผลชนะ–แพ้ และ replay loop.
- **Player-facing Information Pass:** ซ่อน `T...`, event ID, root event, confidence percentage และ suspicion vector จาก UI; ใช้เวลาจริงในโลกเกม, ประโยค Who/What/Where, แหล่งที่มาแบบ “เห็นเอง/ได้ยินจาก” และระดับ “น่าเชื่อถือ/ควรจับตา” พร้อม tutorial ตัวอย่างก่อนเริ่มกะ.
- **Floor-plan & Ending Pass:** เปลี่ยนแผนที่จาก node graph เป็นผังพื้นที่ภายใน/ภายนอกที่อ่านเป็นห้อง ทางเดิน ประตู และจุดใช้งานได้ พร้อมฉากจบสองช่วง (`คำกล่าวหา → ผลที่ตามมา`) ซึ่งอ้างเบาะแสจริงที่เด่นที่สุดของรอบนั้น.
- **Thai Localization Audit:** UI, tooltip, objective, บทสนทนา, แฟ้มคดี และฉากจบรองรับไทย/อังกฤษ; ใช้คำไทยว่า “ผู้ควบคุม” ในเนื้อหาแทนคำระบบ `Player` และเพิ่ม smoke/regression check ป้องกันหัวข้อ journal หรือชื่อ George ภาษาอังกฤษหลุดในโหมดไทย.
- **UI Layout Pass:** แผงขวาเปลี่ยนจากพิกัดตายตัวเป็นการวางต่อกันตามลำดับ (`PanelHeading`/`PanelText`/`PanelButton`) ทำให้องค์ประกอบทับกันไม่ได้อีก — แก้กรณีบล็อก `HOW YOU LOOK` ถูกปุ่มกล่าวหาทับจนมองไม่เห็น, `Label` ไม่ wrap จนข้อความทะลุขออกนอกจอ, ป้ายชื่อห้องถูก token ทับ และห้องว่างขึ้นว่า “0 คน” ทุกห้อง.
- **Overlay Layout Pass:** ปุ่มตัวเลือกเคยตรึงที่ y=310 ตายตัว ทำให้ทุกหน้ามีช่องว่าง 200-400px คั่นกลาง; ตอนนี้วัดความสูงเนื้อหาจริงแล้ววางต่อ, ภาพ portrait แสดงทุกหน้าเพื่อให้คอลัมน์ข้อความไม่กระโดดไปมาระหว่างหน้า และตัวเลือก ≤ 3 ข้อเรียงเต็มความกว้างแทนตารางที่เหลือเศษค้างแถว.
- **Floor Plan Fix:** ห้องใต้ดินกว้าง 10 หน่วยทับห้องกล้องวงจรปิดและห้องผู้จัดการ ส่วนล็อบบี้ทับห้องครัว/ห้อง 201; ปรับขนาดใน `hotel-world.json` จนไม่มีห้องไหนทับกันเลย.
- **Reachability:** ห้องที่เดินไปไม่ได้จะหรี่ลง แทนที่จะต้องคลิกแล้วโดนปฏิเสธถึงจะรู้.
- **Dev tool:** `--capture-ui <path>` เรนเดอร์สองสามเฟรมแล้วบันทึกภาพหน้าจอลงไฟล์ เพื่อตรวจ layout จากของจริงแทนการเดาจากพิกัด (`godot_console --path src/Game.Client.Godot res://Scenes2D/Main2D.tscn -- --capture-ui ui.png [--capture-screen journal|claims|exposure|inspect|deduce|talk] [--thai]`).

### ส่วนที่พักไว้

- **3D Prototype Status:** เก็บ Godot 3D hotel/navigation/HUD เดิมเป็น debug และ regression prototype; ปิด 3D Emotion Bubbles, 3D Interactive Object Nodes และ procedural/spatial audio ไว้ และไม่ขยาย art pipeline ฝั่ง 3D ในช่วง First Fun Playtest.


คำสั่งตรวจสอบระบบ:

```bash
dotnet restore Game.sln
dotnet build Game.sln --configuration Release --no-restore
dotnet test Game.sln --configuration Release --no-build
dotnet run --project tools/SimRunner -- --scenario basement --seed 481516 --ticks 16
dotnet run --project tools/SimRunner -- --scenario rumor-cascade --seed 481516 --ticks 16
dotnet run --project tools/SimRunner -- --scenario deceptive-alibi --seed 481516 --ticks 16
dotnet run --project tools/SimRunner -- --scenario reality-breach --seed 481516 --ticks 16
```

ทั้ง 4 scenario รันด้วย truth เป็น null จึงเป็น regression baseline ที่ตรึงไว้ — '
'`basement`, `rumor-cascade`, `deceptive-alibi` ไม่เคยเปลี่ยนตลอดงานชุดนี้ '
'ส่วน `reality-breach` เปลี่ยนหนึ่งครั้งจากการแยก anomaly tag โดยตั้งใจ '
'(`abc2735c...` → `9857a3b3...`)

Basement scenario รันผ่าน headless SimRunner พร้อม structured summary, JSONL trace,
metrics และ SHA-256 event fingerprint (`9a5605575c7970a907aa649f19f645181c3db30f88fd8746c27201e1846acbb9`)
ส่วน Godot รัน NPC Brain/session ทีละ tick โดยตรง และ commit logical location หลัง character token 2D เดินถึงจริง

เปิด Godot prototype:

```bash
winget install --id GodotEngine.GodotEngine.Mono --exact --version 4.7.2 --scope user
godot --editor --path src/Game.Client.Godot
```

หลังติดตั้งครั้งแรกให้เปิด terminal ใหม่ แล้วตรวจ 2D graybox แบบ headless:

```bash
godot_console --headless --path src/Game.Client.Godot res://Scenes2D/Main2D.tscn -- --smoke-2d
godot_console --headless --path src/Game.Client.Godot res://Scenes2D/Main2D.tscn -- --smoke-2d --thai
```

เล่นหนึ่งคืนเต็มโดยไม่มีคนควบคุม แล้วบันทึกว่าเกมพูดอะไรกลับมาบ้างและตอนไหน:

```bash
godot_console --headless --path src/Game.Client.Godot res://Scenes2D/Main2D.tscn -- --night-report night.md
godot_console --headless --path src/Game.Client.Godot res://Scenes2D/Main2D.tscn -- --night-report night.md --night-seed 11
```

เครื่องมือนี้เดินบน client code path เดียวกับที่ผู้เล่นใช้ ด้วยพฤติกรรม “ผู้เล่นที่อยู่ตรงนั้นจริง”
(ถาม–ดูของ–เดิน สลับกันทุก 9 นาทีในเกม) และเขียนตารางความหนาแน่นรายชั่วโมง, ช่วงที่เกม
เงียบยาวที่สุด, จังหวะที่มิเตอร์แต่ละตัวขยับ และฉากจบที่ได้ ผลรอบแรกอยู่ใน
`playtest/first-night-findings.md`

การทดสอบสำเร็จเมื่อ process คืน exit code `0` และแสดง `HOTEL_2D_SMOKE_PASS` โดยครอบคลุม
การโหลด content, แผนที่ 8 ห้อง, ตัวละคร 6 คน, click-to-move, movement acknowledgement,
บทสนทนา, การตรวจวัตถุ, แฟ้มคดี และ localization ไทย/อังกฤษ.

สร้าง Windows playtest package สำหรับผู้ทดสอบภายนอก:

```powershell
.\scripts\Build-Playtest.ps1 -GodotPath "C:\Path\To\godot.exe"
```

สคริปต์จะ build/test solution, export preset `Windows Desktop`, สร้าง zip พร้อม
คู่มือผู้เล่นและ feedback template จาก `playtest/` โดยไม่รวม source code ใน package;
moderator ใช้ `playtest/protocol.md` จาก repository แยกต่างหาก ผู้ทดสอบต้องใช้ Godot 4.7.2 .NET export templates บนเครื่อง build เท่านั้น
ติดตั้งผ่าน Godot Editor > Editor > Manage Export Templates ก่อนรันสคริปต์ครั้งแรก

เอกสารการทดลองอยู่ในโฟลเดอร์ [playtest](playtest/README.md) โดย `README.md` สำหรับผู้เล่น,
`protocol.md` สำหรับ moderator และ `feedback-template.md` สำหรับแบบประเมินหลังเล่น

ฉาก 3D เดิมยังเรียกตรวจ regression ได้ด้วย:

```bash
godot_console --headless --path src/Game.Client.Godot res://Scenes/Main.tscn --fixed-fps 60 --quit-after 4000 -- --smoke-playthrough
```

---

## 0. Game Concept และที่มาของระบบ

## 0.1 แนวคิดหลักของเกม

เกมนี้เริ่มจากแนวคิดว่า:

> **ตัวละครในโลกเกมไม่รู้ว่าตัวเองเป็น NPC และพยายามค้นหาว่าใครคือ “Player” ที่แฝงตัวอยู่ในโลกเดียวกับพวกเขา**

ผู้เล่นรับบทเป็นหนึ่งในตัวละครของโลกนั้น โดยในช่วงต้นเกมจะยังไม่สามารถเชื่อได้อย่างเต็มที่ว่า:

- ตัวเองเป็น NPC จริงหรือไม่
- ใครในโลกกำลังถูก Player ควบคุม
- พฤติกรรมผิดปกติที่พบเป็นพฤติกรรมของ Player จริง หรือเป็นเพียงความลับของ NPC ธรรมดา
- โลกที่กำลังอยู่เป็น “โลกจริง” สำหรับตัวละคร หรือเป็นระบบเกมที่กำลังเริ่มพัง

Fantasy หลักของเกมจึงไม่ใช่เพียง:

> “ตามหาคนร้าย”

แต่คือ:

> **“ฉันเป็นคนในโลกที่เริ่มสงสัยว่าความจริงรอบตัวอาจเป็นเพียงระบบเกม”**

---

## 0.2 Core Gameplay Idea

ในโลกจะมี NPC หลายตัวที่ใช้ชีวิตตาม:

- บทบาท
- ตารางเวลา
- ความต้องการ
- ความสัมพันธ์
- บุคลิก
- ความลับส่วนตัว

ตัวอย่าง:

```text
08:00 ตื่น
08:30 กินอาหาร
09:00 ไปทำงาน
12:00 พัก
13:00 ทำงาน
18:00 เวลาส่วนตัว
22:00 กลับห้อง
23:00 นอน
```

NPC ปกติควรมีพฤติกรรมที่คาดเดาได้ “พอประมาณ”

สิ่งนี้สำคัญ เพราะผู้เล่นจะสามารถสังเกตได้ว่า:

> “คนนี้ไม่ควรอยู่ตรงนี้ในเวลานี้”

หรือ:

> “ทำไมเขาถึงทำสิ่งเดิมซ้ำ ๆ ทั้งที่ไม่มีเหตุผล?”

---

## 0.3 Player ที่ซ่อนอยู่ในโลก

ในแต่ละรอบ จะมี NPC อย่างน้อยหนึ่งตัวที่มีพฤติกรรมคล้าย “มนุษย์กำลังเล่นเกม”

ตัวอย่าง Player Archetype:

### Explorer

```text
- เข้าไปในพื้นที่ที่ยังไม่เคยไป
- เปิดประตูทุกบาน
- สำรวจพื้นที่ที่ไม่มีเหตุผลทาง role
- พยายามหาทางออกนอกเส้นทางปกติ
```

### Completionist

```text
- คุยกับทุกคน
- ตรวจทุก object
- เก็บ item ทุกอย่าง
- พยายาม exhaust interaction
```

### Chaos Player

```text
- ทำ interaction ที่ไม่มีเหตุผล
- เปิด/ปิดประตูซ้ำ
- ย้ายของ
- ทดสอบระบบโลก
```

### Roleplayer

```text
- พยายามทำตัวเหมือน NPC ปกติ
- ทำงานตาม schedule
- ลดพฤติกรรมที่ทำให้ถูกสงสัย
```

Player AI ไม่ควรมี flag ที่ NPC สามารถอ่านได้ว่า:

```text
IsPlayer = true
```

ทุกระบบต้องอนุมานจาก **พฤติกรรมที่เกิดขึ้นจริงเท่านั้น**

---

## 0.4 สิ่งที่ทำให้เกมไม่ใช่ Social Deduction ธรรมดา

เกมไม่ได้มีเพียงคำถามว่า:

> “ใครคือ Player?”

แต่มีคำถามพร้อมกันหลายระดับ:

```text
ใครคือ Player?
↓
เหตุการณ์ที่เห็นเป็นความจริงหรือไม่?
↓
NPC คนนี้แปลกเพราะเป็น Player หรือมีความลับ?
↓
คนที่เล่าเรื่องนี้น่าเชื่อถือแค่ไหน?
↓
ฉันเองกำลังทำตัวเหมือน Player หรือเปล่า?
↓
โลกนี้มีกฎอะไรที่พวกเรายังไม่เข้าใจ?
```

ดังนั้นเกมต้องสร้าง **ความไม่แน่นอนจากข้อมูลที่ไม่สมบูรณ์** ไม่ใช่จาก RNG อย่างเดียว

---

## 0.5 False Positive คือส่วนสำคัญของเกม

NPC ธรรมดาต้องสามารถทำสิ่งน่าสงสัยได้

ตัวอย่าง:

```text
Thief
→ แอบเข้าไปในห้องคนอื่น

Secret Relationship
→ นัดเจอกันกลางคืน

Night Owl
→ เดินในเวลาที่คนอื่นนอน

Liar
→ ให้ข้อมูลไม่ตรงกับเหตุการณ์จริง

Forgetful
→ จำรายละเอียดคลาดเคลื่อน
```

ทำให้ผู้เล่นไม่สามารถใช้กฎง่าย ๆ ว่า:

> “ใครทำตัวผิดปกติ = Player”

นี่คือเหตุผลที่ระบบ Suspicion ใน Technical Design ต้องแยก:

```text
Criminality
Secrecy
RoleDeviation
MetaBehavior
ImpossibleBehavior
Deception
```

ออกจากกัน

---

## 0.6 Reality Anomalies

เมื่อพัฒนา core simulation ได้แล้ว เกมสามารถเพิ่มเหตุการณ์ที่ NPC ตีความไม่เหมือนผู้เล่นภายนอก

ตัวอย่าง:

```text
Save Reload
NPC interpretation:
"เหตุการณ์ย้อนกลับ"

Fast Travel
NPC interpretation:
"The Blink"

Respawn
NPC interpretation:
"The Returning"

Item Respawn
NPC interpretation:
"ของกลับมาอยู่ที่เดิมเอง"

Dialogue Reset
NPC interpretation:
"คนคนนี้จำบทสนทนาเมื่อครู่ไม่ได้"
```

สำหรับมนุษย์ที่เล่นเกม สิ่งเหล่านี้คือ mechanic ปกติ

แต่สำหรับ NPC ที่คิดว่าโลกของตัวเองเป็นจริง มันคือปรากฏการณ์เหนือธรรมชาติ

ระบบเหล่านี้ยังไม่ใช่ MVP แต่เป็น direction สำคัญที่ architecture ต้องรองรับ

---

## 0.7 Deviation — ผู้เล่นเองก็ถูกโลกสังเกต

อีก mechanic หลักที่ตั้งใจไว้คือ:

> **ยิ่งผู้เล่นพยายามสืบความจริงมากเท่าไร ผู้เล่นก็ยิ่งทำตัวผิดจากบทบาท NPC ของตัวเองมากขึ้น**

ตัวอย่างผู้เล่นมี role เป็น Receptionist:

```text
Expected:
- อยู่ Lobby
- ให้บริการแขก
- ตรวจงานตามเวลา

Actual:
- ตาม George ไป Basement
- เปิดห้องแขก
- ตรวจตู้ทุกใบ
- หายจากงานนาน
```

NPC อื่นควรสามารถสร้าง observation และ suspicion ต่อผู้เล่นได้เช่นเดียวกัน

ดังนั้น architecture ต้องไม่มี “NPC AI” กับ “Human Player” เป็นคนละโลกของระบบ

มนุษย์ควรสร้าง event ผ่าน interaction pipeline เดียวกับ NPC:

```text
Human Action
    ↓
World Event
    ↓
NPC Perception
    ↓
Memory
    ↓
Suspicion
```

นี่ทำให้เกิด tension สำคัญ:

> **เพื่อค้นหา Player ผู้เล่นอาจต้องทำสิ่งที่ทำให้ตัวเองดูเหมือน Player**

---

## 0.8 Emergent Storytelling

เป้าหมายไม่ได้อยู่ที่สร้าง quest script จำนวนมาก

เป้าหมายคือให้ระบบสามารถสร้าง chain เช่น:

```text
George เข้า Basement ตอนกลางคืน
        ↓
Anna เห็น
        ↓
Anna สงสัย George
        ↓
Anna บอก Bob
        ↓
Bob เชื่อ Anna
        ↓
Bob ตาม George
        ↓
George สังเกตเห็น Bob ตาม
        ↓
George เริ่มสงสัย Bob
        ↓
George บอก Charlie
        ↓
Charlie เชื่อ George มากกว่า Anna
```

โดยไม่มี script ที่กำหนดเรื่องราว chain นี้ไว้ล่วงหน้า

นี่คือ **Emergent Narrative** ที่เป็นแกนหลักของเกม

---

## 0.9 Prototype Setting

เพื่อให้ทดสอบระบบง่าย Prototype แรกจะใช้พื้นที่เล็ก เช่น:

> **โรงแรมขนาดเล็ก**

เหตุผล:

- ทุกคนมีเหตุผลที่จะอยู่ในพื้นที่เดียวกัน
- มี room ownership ชัด
- มีพื้นที่ restricted
- มี routine ที่เข้าใจง่าย
- เห็น role deviation ได้ง่าย
- สร้าง secret behavior ได้ง่าย

Prototype อาจประกอบด้วย:

```text
6 NPC
1 Human Player
1 Hidden Player AI

Lobby
Kitchen
Dining Room
Hallway
Guest Rooms
Basement
```

แต่ Technical Foundation ต้องไม่ผูกกับโรงแรมโดยตรง เพื่อให้ในอนาคตเปลี่ยนเป็น:

```text
หมู่บ้าน
สถานีอวกาศ
เรือ
สำนักงาน
เมืองเล็ก
เรือนจำ
```

ได้โดยไม่แก้ core architecture

---

## 0.10 Design Pillars

ระบบทั้งหมดในเอกสารนี้มีที่มาจาก Design Pillars 5 ข้อ:

### 1. Observable Normality

ผู้เล่นต้องเรียนรู้ได้ว่า “ปกติ” คืออะไร

ถ้า NPC random ตลอดเวลา จะไม่มีสิ่งที่เรียกว่า “ผิดปกติ”

---

### 2. Imperfect Information

NPC แต่ละคนต้องเห็นโลกไม่เท่ากัน

```text
World Truth != Individual Truth
```

นี่คือเหตุผลของ Perception + Memory

---

### 3. Explainable Suspicion

ทุกความสงสัยต้องมีเหตุผลย้อนกลับไปหา evidence ได้

ไม่ใช้:

```text
Suspicion += random
```

นี่คือเหตุผลของ Evidence Contribution

---

### 4. Player-like Behavior is a Pattern

การเปิดลิ้นชักหนึ่งครั้งไม่แปลก

แต่การเปิดลิ้นชักทุกใบในอาคารภายใน 2 นาทีอาจแปลก

นี่คือเหตุผลของ Behavior Pattern Detector

---

### 5. Systems Create Stories

ระบบควรสร้างสถานการณ์ใหม่จาก feedback loop ของมันเอง

```text
Action
↓
Event
↓
Perception
↓
Memory
↓
Suspicion
↓
Decision
↓
New Action
```

นี่คือเหตุผลที่ Technical Design ในเอกสารนี้ให้ความสำคัญกับ simulation มากกว่า UI

---

## 0.11 เป้าหมายระยะยาวของ Concept

ถ้า system foundation ทำงานได้ เกมสามารถขยายไปสู่คำถามเชิง narrative เช่น:

> NPC ที่รู้ว่าตัวเองเป็น NPC ยังถือว่าเป็น NPC อยู่หรือไม่?

หรือ:

> ถ้า NPC สามารถจำเหตุการณ์หลัง Save Reload ได้ ใครกันแน่ที่กำลังควบคุมโลก?

แต่ทั้งหมดนี้เป็น layer ที่สร้างภายหลัง

เป้าหมายระยะปัจจุบันคือสร้างระบบที่สามารถทำให้ผู้เล่นเกิดความรู้สึก:

> **“เดี๋ยวนะ... ทำไมคนนี้ถึงทำแบบนั้น?”**

โดยระบบสร้างเหตุผลของพฤติกรรมนั้นขึ้นมาจริง ๆ ไม่ใช่เพียง script เพื่อหลอกผู้เล่น

---

# 1. Executive Summary

เกมนี้ควรถูกพัฒนาแบบ **simulation-first** ไม่ใช่ UI-first

สิ่งที่ต้องพิสูจน์ก่อนคือ:

1. NPC สามารถใช้ชีวิตตาม routine ได้เอง
2. โลกสร้างเหตุการณ์ที่เป็น “ความจริง” เพียงชุดเดียว
3. NPC แต่ละตัวรับรู้เหตุการณ์ต่างกันตามตำแหน่ง/สถานการณ์
4. NPC จำสิ่งที่เห็นหรือได้ยินเป็น memory ของตัวเอง
5. NPC ตีความ memory แล้วเกิด suspicion ต่อคนอื่น
6. Suspicion เปลี่ยนพฤติกรรมของ NPC เช่น เฝ้าดู ตาม สอบถาม หรือบอกคนอื่น
7. ระบบสามารถสร้างสถานการณ์ที่ผู้พัฒนาไม่ได้ script ไว้โดยตรง
8. ทุกสถานการณ์ต้อง reproduce ได้ด้วย seed และ event log

### Tech Stack ที่แนะนำ

**Engine:** Godot 4.7.2 .NET  
**Language:** C# บน .NET 8  
**Core Simulation:** Pure C# class library แยกจาก Godot  
**Testing:** xUnit + deterministic scenario runner  
**Data:** JSON + strongly typed C# definitions  
**Logging:** JSONL / structured event log  
**Version Control:** Git  
**Quality Gate:** ตรวจในเครื่องด้วย `dotnet build`, `dotnet test` และ `dotnet format`; ปัจจุบันยังไม่ได้ตั้งค่า CI workflow
**Target แรก:** Desktop — Windows/Linux/macOS

แนวทางสำคัญคือ:

```text
Godot = World / Navigation / Rendering / Input

Pure C# Simulation =
NPC Decisions
Events
Perception
Memory
Beliefs
Suspicion
Rules
Deterministic RNG
```

ห้ามให้ core simulation พึ่ง `Node`, `SceneTree`, physics หรือ rendering API ของ Godot โดยตรง

ผลคือเราจะสามารถรัน:

```bash
dotnet test
```

และจำลอง NPC หลายร้อยรอบโดยไม่เปิดเกมจริงได้

---

# 2. ทำไมเลือก Godot + C#

## 2.1 Godot เหมาะกับเกมนี้

เกมนี้ไม่ได้มี bottleneck หลักอยู่ที่กราฟิกระดับ AAA

ความยากจริงอยู่ที่:

- simulation
- emergent behavior
- NPC state
- event propagation
- debugging
- iteration speed
- navigation
- world interaction

Godot เหมาะเพราะตัว engine เบา, iteration เร็ว และระบบ Scene/Node ใช้เป็น presentation layer ได้ดีโดยไม่ต้องเอา business logic ทั้งหมดไปผูกกับ engine

ณ วันที่จัดทำเอกสาร Godot 4.7.2 เป็น stable release ล่าสุดในสาย 4.x

## 2.2 ทำไมใช้ C# แทน GDScript เป็นแกนหลัก

GDScript เหมาะมากกับ gameplay script และ iteration ภายใน Godot

แต่โปรเจกต์นี้มี logic ที่:

- มี data model จำนวนมาก
- ต้อง deterministic
- ต้อง unit test จำนวนมาก
- มี event pipeline
- มี generic collections
- ต้อง refactor architecture บ่อย
- มี simulation ที่อยากรันแบบ headless
- มีโอกาสทำ tooling ภายนอก engine ในอนาคต

ดังนั้น C# ให้ประโยชน์ด้าน:

- static typing
- IDE/refactoring
- test ecosystem
- generic/data structures
- pure .NET library
- profiling
- maintainability

### ข้อจำกัดสำคัญ

Godot 4 + C# ยังไม่เหมาะถ้าเป้าหมายหลักคือ Web export

ดังนั้น roadmap นี้กำหนด **Desktop เป็น platform แรก**

ถ้าภายหลัง Web กลายเป็น requirement หลัก ให้ประเมินใหม่ระหว่าง:

- ย้าย gameplay-facing layer ไป GDScript
- ทำ simulation core แยกและสร้าง adapter ใหม่
- หรือเปลี่ยน engine

---

# 3. Tech Stack Decision

| ส่วน | เลือก | เหตุผล |
|---|---|---|
| Engine | Godot 4.7.2 .NET | เบา, iteration เร็ว, 3D/navigation เพียงพอ |
| Language | C# | เหมาะกับระบบ simulation และ automated tests |
| Runtime | .NET 8 | สอดคล้องกับ Godot 4.7 |
| Simulation | Pure C# | ทดสอบโดยไม่เปิด Godot |
| Unit Test | xUnit | mature, simple, CI-friendly |
| Serialization | System.Text.Json | ไม่มี dependency เพิ่มใน MVP |
| Rule Config | JSON | อ่านง่าย, diff ง่าย, engine-independent |
| Log | JSONL | stream ได้, grep ง่าย, วิเคราะห์ภายหลังง่าย |
| Source Control | Git | standard |
| Binary Asset | Git LFS เฉพาะเมื่อจำเป็น | อย่าเอา source/data เข้า LFS |
| Quality Gate | Local .NET CLI | build, test และตรวจ format ก่อน commit; ยังไม่ได้ตั้งค่า CI workflow |
| IDE | Rider / Visual Studio / VS Code | ใช้ C# tooling ภายนอก Godot |
| Database | ยังไม่ใช้ | ไม่จำเป็นใน MVP |
| LLM | ยังไม่ใช้ | ป้องกัน non-determinism และ scope creep |
| ECS | ยังไม่ใช้ | NPC ช่วงแรกมีไม่มากและ complexity ไม่คุ้ม |

---

# 4. Architectural Principles

## 4.1 World Truth กับ NPC Truth ต้องแยกกัน

หัวใจของเกมคือ:

```text
World Truth != NPC Belief
```

ตัวอย่าง:

```text
World Truth:
George เข้า Basement เวลา 01:14

Anna:
เห็น George ชัดเจน
confidence = 0.95

Bob:
ได้ยินประตู แต่ไม่เห็นว่าใคร
confidence = 0.40

Charlie:
ไม่ได้รับรู้เหตุการณ์

David:
Bob เล่าให้ฟังภายหลัง
confidence = 0.25
```

ถ้าทุก NPC อ่าน global state โดยตรง เกม deduction จะพังทันที

---

## 4.2 Event เป็น Immutable Fact

เมื่อ action ที่สำคัญเกิดขึ้น ให้สร้าง `WorldEvent`

หลังจากสร้างแล้วห้ามแก้ข้อมูลของ event

```csharp
public sealed record WorldEvent(
    EventId Id,
    SimTime Time,
    EntityId Actor,
    EventType Type,
    EntityId? Target,
    LocationId Location,
    IReadOnlySet<EventTag> Tags,
    EventPayload Payload
);
```

ระบบอื่นอ่าน event แต่ไม่แก้มัน

---

## 4.3 Simulation ต้อง Deterministic

ถ้าใช้:

```text
seed = 481516
```

และ input เหมือนเดิม

ผล simulation ควรเหมือนเดิมทุกครั้ง

Random ทั้งหมดต้องผ่าน interface เดียว:

```csharp
public interface ISimRandom
{
    int NextInt(int min, int max);
    float NextFloat();
    bool Chance(float probability);
}
```

ห้ามเรียก random จาก engine ตรง ๆ ภายใน core

---

## 4.4 Composition over Inheritance

ห้ามสร้าง class tree แบบ:

```text
NPC
├── ThiefNPC
├── LiarNPC
├── PlayerNPC
└── ReceptionistNPC
```

ใช้:

```text
NPC
+ Role
+ Traits
+ Secret
+ Schedule
+ GoalSources
+ PerceptionProfile
+ MemoryProfile
```

ตัวเดียวสามารถเป็น:

```text
Role        = Receptionist
Trait       = Nervous
Trait       = Observant
Secret      = Thief
Schedule    = NightShift
```

---

## 4.5 Logic ไม่ขึ้นกับ Frame Rate

Rendering อาจ 60/120 FPS

แต่ simulation ใช้ fixed logical tick เช่น:

```text
Simulation Tick = 4 Hz
Decision Tick   = 2 Hz
Perception      = event-driven
```

อย่าให้ NPC brain ตัดสินใจใหม่ทุก render frame

---

# 5. Repository Structure

แนะนำ repository เดียวแต่แยก projects

```text
game/
├── src/
│   ├── Game.Sim/
│   │   ├── Cases/          # SessionTruth / CaseGenerator
│   │   ├── Schedules/      # HotelNightRoutines / SecretStaging / SocialGraph / Needs
│   │   ├── Entities/
│   │   ├── Time/
│   │   ├── Events/
│   │   ├── Perception/
│   │   ├── Memory/
│   │   ├── Beliefs/
│   │   ├── Suspicion/
│   │   ├── Brain/
│   │   ├── Actions/
│   │   ├── Rules/
│   │   └── Random/
│   │
│   ├── Game.Content/
│   │   ├── Data/
│   │   ├── Roles/
│   │   ├── Traits/
│   │   ├── Schedules/
│   │   └── SuspicionRules/
│   │
│   └── Game.Client.Godot/
│       ├── Scenes/
│       ├── Scripts/
│       ├── Adapters/
│       ├── Navigation/
│       ├── Interaction/
│       └── Debug/
│
├── tests/
│   ├── Game.Sim.Tests/
│   └── Game.Scenarios.Tests/
│
├── tools/
│   └── SimRunner/
│
├── logs/
│   └── .gitkeep
│
├── docs/
│
└── Game.sln
```

---

# 6. Core Domain Model

NPC ตัวหนึ่งไม่ควรเป็น Godot Node ในระดับ domain

```csharp
public sealed class NpcState
{
    public EntityId Id { get; init; }

    public RoleId Role { get; set; }

    public LocationId LogicalLocation { get; set; }

    public ScheduleState Schedule { get; init; }

    public NeedState Needs { get; init; }

    public TraitSet Traits { get; init; }

    public MemoryStore Memory { get; init; }

    public SuspicionStore Suspicion { get; init; }

    public GoalState Goals { get; init; }

    public ActionState CurrentAction { get; set; }
}
```

Godot มีตัวแทนอีกชั้น:

```text
NpcActorNode
    ↓
NpcAdapter
    ↓
NpcState
```

`NpcActorNode` รับผิดชอบ:

- model/animation
- pathfinding
- physical movement
- interaction animation

`NpcState` รับผิดชอบ:

- คิด
- จำ
- เชื่อ
- สงสัย
- เลือก intent

---

# 7. Simulation Loop

แนะนำ flow:

```text
Clock Advance
    ↓
Complete Actions
    ↓
Generate World Events
    ↓
Perception
    ↓
Create Observations
    ↓
Memory Update
    ↓
Belief / Suspicion Update
    ↓
Needs + Schedule Update
    ↓
Goal Generation
    ↓
Utility Evaluation
    ↓
Select Intent
    ↓
Issue Action Commands
```

Pseudo-code:

```csharp
public void Tick(SimDelta dt)
{
    _clock.Advance(dt);

    _actions.ResolveCompletedActions();
    _events.Flush();

    _perception.ProcessPendingEvents();
    _memory.ProcessObservations();
    _suspicion.RecalculateDirtyCases();

    _needs.Update(dt);
    _schedule.Update(_clock);

    _brains.EvaluateDueAgents();
    _actions.DispatchNewCommands();
}
```

---

# 8. Event System

## 8.1 Event Categories

เริ่มจาก event จำนวนน้อยก่อน

```text
Movement
- EnterLocation
- LeaveLocation

Interaction
- OpenDoor
- InspectObject
- TakeItem
- PutItem

Social
- StartConversation
- ShareInformation
- Accuse

Role
- BeginWork
- MissWork
- EnterRestrictedArea

Meta Pattern
- RepeatInteraction
- LootSweep
- DialogueExhaustion
- BoundaryTesting
```

อย่าสร้าง event type หลายร้อยตัวตั้งแต่แรก

---

## 8.2 Event Bus

Core ใช้ synchronous deterministic event queue ก่อน

```csharp
public interface IEventStream
{
    void Publish(WorldEvent evt);
    IReadOnlyList<WorldEvent> Drain();
}
```

ไม่จำเป็นต้องใช้ async/message broker

เหตุผล:

- simulation อยู่ process เดียว
- ต้อง deterministic
- debug ง่ายกว่า
- ordering ชัดเจน

---

## 8.3 Event Log

ทุก event สำคัญเขียนเป็น JSONL

```json
{"tick":1402,"time":"01:14:00","type":"EnterLocation","actor":"george","location":"basement"}
{"tick":1402,"type":"ObservationCreated","observer":"anna","source":"evt-91","confidence":0.94}
{"tick":1402,"type":"MemoryCreated","npc":"anna","memory":"mem-55"}
```

Event log มีประโยชน์สำหรับ:

- reproduce bug
- inspect simulation
- compare balancing
- analyze player-like patterns
- future replay
- automated metrics

---

# 9. Perception System

Perception ไม่ควรเริ่มจาก realistic vision cone เต็มรูปแบบ

MVP ใช้ logical location ก่อน

```text
same room         → high confidence
adjacent room     → audio only
closed door       → reduce audio
dark room         → reduce visual
sleeping          → ignore most events
```

API:

```csharp
public interface IPerceptionResolver
{
    IEnumerable<Observation> Observe(
        NpcState observer,
        WorldEvent evt,
        WorldSnapshot world
    );
}
```

`Observation`:

```csharp
public sealed record Observation(
    ObservationId Id,
    EventId SourceEvent,
    EntityId Observer,
    EntityId? PerceivedActor,
    EventType PerceivedType,
    LocationId? Location,
    float Confidence,
    float Salience,
    PerceptionChannel Channel
);
```

### สำคัญ

`Observation` อาจไม่ตรงกับ `WorldEvent`

เช่น:

```text
World:
George เปิดประตู

Bob:
ได้ยิน “ประตูเปิด”
Actor = Unknown
```

นี่คือ mechanic ไม่ใช่ bug

---

# 10. Memory System

Memory แบ่งเป็น:

```text
Episodic
- สิ่งที่ NPC เห็น/ได้ยินเอง

Social
- สิ่งที่คนอื่นบอก

Belief
- ข้อสรุปที่ NPC สร้างขึ้น
```

MVP เริ่มแค่ Episodic + Social ก่อน

```csharp
public sealed class MemoryRecord
{
    public MemoryId Id { get; init; }

    public MemoryKind Kind { get; init; }

    public EntityId? Subject { get; init; }

    public EventType EventType { get; init; }

    public LocationId? Location { get; init; }

    public SimTime Time { get; init; }

    public float Confidence { get; set; }

    public float Salience { get; init; }

    public EntityId? InformationSource { get; init; }

    public EventId RootEventId { get; init; }
}
```

## 10.1 RootEventId

จำเป็นต้องมีเพื่อป้องกัน rumor feedback loop

```text
Anna เห็น George
 ↓
Anna บอก Bob
 ↓
Bob บอก Charlie
 ↓
Charlie บอก Anna
```

ทั้ง chain ต้องมี:

```text
RootEventId = evt_original
```

จึงรู้ว่าข่าวทั้งหมดมาจาก source เดียว

---

## 10.2 Memory Decay

เริ่มจากสูตรง่าย:

```text
retainedConfidence =
initialConfidence
× exp(-decayRate × age)
```

Trait เปลี่ยนค่า:

```text
Forgetful  → decay เร็ว
Observant  → initial confidence สูง
Paranoid   → suspicious memory salience สูง
```

MVP ไม่ต้อง simulate memory distortion ซับซ้อน

---

# 11. Suspicion System

ห้ามใช้ค่าเดียว:

```text
Suspicion = 73
```

ใช้ vector:

```csharp
public sealed class SuspicionVector
{
    public float Criminality { get; set; }
    public float Secrecy { get; set; }
    public float RoleDeviation { get; set; }
    public float MetaBehavior { get; set; }
    public float ImpossibleBehavior { get; set; }
    public float Deception { get; set; }
}
```

เหตุผลคือ:

```text
ขโมย
Criminality = สูง
Player-like = ต่ำ

Completionist
Criminality = ต่ำ
MetaBehavior = สูง
RoleDeviation = สูง
```

ทำให้ “คนมีความลับ” ไม่เท่ากับ “Player”

---

## 11.1 Suspicion ต้อง Derived จาก Evidence

ห้าม:

```csharp
npc.Suspicion += 10;
```

ใช้:

```text
Memory
  ↓
SuspicionRule
  ↓
EvidenceContribution
  ↓
SuspicionCase
```

```csharp
public sealed record EvidenceContribution(
    MemoryId SourceMemory,
    SuspicionDimension Dimension,
    float Strength,
    float Confidence
);
```

คะแนนจริง:

```text
effective =
strength
× confidence
× memoryRetention
```

ข้อดีคือ debug ได้ว่าคะแนนมาจากไหน

---

# 12. Suspicion Rules แบบ Data-Driven

ตัวอย่าง JSON:

```json
{
  "id": "restricted_area_without_role",
  "match": {
    "event": "EnterLocation",
    "requiredTags": ["Restricted"],
    "actorRoleAllowed": false
  },
  "effects": {
    "roleDeviation": 20,
    "secrecy": 8
  }
}
```

อีก rule:

```json
{
  "id": "loot_sweep",
  "match": {
    "event": "BehaviorPattern",
    "pattern": "LootSweep"
  },
  "effects": {
    "metaBehavior": 35,
    "roleDeviation": 15
  }
}
```

Design goal คือ balance โดยแก้ data มากกว่าแก้ source code

---

# 13. Behavior Pattern Detector

นี่เป็นระบบสำคัญกว่าการทำ AI ซับซ้อน

Action เดี่ยวมักไม่บอกว่าใครเป็น Player

Pattern ต่างหากที่บอก

ตัวอย่าง:

```text
เปิดลิ้นชัก 1 ครั้ง
→ ปกติ

เปิดลิ้นชัก 11 อันใน 90 วินาที
→ LootSweep
```

MVP Patterns:

1. `LootSweep`
2. `DialogueExhaustion`
3. `RepeatedInteraction`
4. `RoleNeglect`
5. `RestrictedAreaTour`
6. `BoundaryTesting`

API:

```csharp
public interface IBehaviorPatternDetector
{
    IEnumerable<BehaviorPatternEvent> Process(
        EntityId actor,
        IReadOnlyList<WorldEvent> recentEvents
    );
}
```

ไม่ต้องใช้ ML

rule-based detector จะ:

- deterministic
- explainable
- balance ง่าย
- test ง่าย

---

# 14. NPC Brain

MVP ใช้ Utility AI

ไม่ใช้ Behavior Tree framework

Goal sources:

```text
ScheduleGoalSource
NeedGoalSource
RoleGoalSource
SecretGoalSource
SuspicionGoalSource
SocialGoalSource
```

Candidate:

```csharp
public sealed record GoalCandidate(
    GoalType Type,
    EntityId? Target,
    float BaseUtility,
    IReadOnlyList<UtilityReason> Reasons
);
```

คะแนน:

```text
score =
baseUtility
+ scheduleWeight
+ needWeight
+ personalityModifier
+ suspicionModifier
+ seededRandomNoise
```

ตัวอย่าง:

```text
Work               72
Eat                24
Follow George      66
Talk to Anna       31
Secret Meeting     81
```

เลือก `Secret Meeting`

---

# 15. Player AI ยังไม่ต้องทำก่อน

ลำดับที่ถูกต้อง:

```text
Normal NPC
↓
Normal NPC + Secret
↓
NPC Suspicion
↓
NPC reacts to Suspicion
↓
Behavior Pattern Detection
↓
ค่อยเพิ่ม Player AI Archetypes
```

เหตุผล:

ถ้าโลกปกติยังไม่น่าเชื่อ

เราจะไม่สามารถรู้ได้ว่า Player behavior “ผิดปกติ” จริงหรือไม่

---

# 16. Godot Adapter Layer

Godot ต้องไม่เป็น source of truth ของ simulation

ตัวอย่าง:

```text
Core:
ActionCommand
MoveTo(LocationId.Basement)

        ↓

Godot Adapter:
lookup marker
NavigationAgent3D.SetTargetPosition(...)

        ↓

เมื่อถึง:
ActionResult.Completed

        ↓

Core:
สร้าง EnterLocation Event
```

ไม่ควรให้ core อ่าน:

```text
NavigationAgent3D.Position
```

ตรง ๆ

ใช้ interface:

```csharp
public interface IWorldActionExecutor
{
    void Execute(ActionCommand command);
}
```

---

# 17. Logical Location Model

เพื่อให้ simulation headless ได้

แยก:

```text
LogicalLocation
```

ออกจาก world coordinate

```text
Lobby
HallwayA
Kitchen
Room201
Basement
```

Core สนใจ:

```text
George อยู่ Basement
Anna อยู่ HallwayA
```

Godot สนใจ:

```text
Vector3
NavigationMesh
Door collision
animation
```

นี่ทำให้ Basement Test รันโดยไม่มี 3D world ได้

---

# 18. Debug โดยไม่ทำ UI เยอะ

ผู้ใช้ไม่ต้องเห็น debug UI ใหญ่

Developer MVP ใช้:

### Console Log

```text
[01:14] George -> ENTER Basement
[01:14] Anna OBSERVED George (0.94)
[01:14] Anna MEMORY mem_55
[01:14] Anna suspicion George RoleDeviation +18
```

### Hotkeys

```text
F1 = pause sim
F2 = speed x2
F3 = speed x10
F4 = dump NPC state
F5 = dump event log
```

### Optional Minimal Overlay

เฉพาะ dev:

```text
Seed: 481516
Time: 01:14
NPCs: 6
Events: 391
```

ไม่ต้องสร้าง investigation UI ใน phase นี้

---

# 19. Automated Scenario Tests

เกม systemic ต้องมี scenario tests

## Test 1 — Basement Test

Setup:

```text
George = Hallway
Anna   = Hallway
Bob    = Kitchen

Basement = Restricted
```

Action:

```text
George enters Basement
```

Expected:

```text
Anna:
Memory exists
confidence > 0.8
George RoleDeviation suspicion > 0

Bob:
No direct memory
George suspicion = 0
```

Anna shares information with Bob

Expected:

```text
Bob:
SocialMemory exists
InformationSource = Anna
RootEvent = original EnterBasement event
confidence < Anna confidence
George suspicion > 0
```

---

## Test 2 — Rumor Loop

```text
Anna → Bob → Charlie → Anna
```

Expected:

```text
RootEventId เท่าเดิมทุก memory
```

และ Anna ห้ามนับ event เดียวเป็นหลักฐานอิสระ 3 ชิ้น

---

## Test 3 — False Positive

NPC ปกติที่เป็น Thief:

```text
Criminality สูง
Secrecy สูง
MetaBehavior ต่ำ
```

Expected:

```text
ระบบไม่ classify เป็น Player-like สูงเกิน threshold
```

---

## Test 4 — Gamer Pattern

NPC เปิด container 10 จุดในเวลาสั้น

Expected:

```text
LootSweep emitted exactly once
MetaBehavior เพิ่ม
```

---

## Test 5 — Determinism

Run:

```text
seed = 12345
scenario = basement
ticks = 10000
```

สองครั้ง

Expected:

```text
event stream hash เหมือนกัน
final state hash เหมือนกัน
```

---

# 20. SimRunner

สถานะ: Basement scenario ใช้งานได้แล้วทั้ง structured summary, JSONL trace,
metrics และ deterministic event fingerprint

สร้าง console app:

```bash
dotnet run --project tools/SimRunner \
  --scenario basement \
  --seed 481516 \
  --ticks 10000 \
  --trace logs/basement.jsonl
```

Output:

```text
NPC count: 6
Ticks: 50000
World events: 2831
Observations: 1910
Memories: 1722
Suspicion changes: 620
Patterns: 31
Runtime: ...
```

และสามารถ:

```bash
--repeat 1000
```

เพื่อดู distribution จาก seed ต่อเนื่องโดยไม่เปิด Godot ถ้าต้องการ trace หลายรอบ
ให้ใช้ placeholder เช่น `--trace logs/basement-{run}.jsonl`

นี่เป็นหนึ่งใน tool ที่มี ROI สูงที่สุดของโปรเจกต์

---

# 21. Metrics ที่ควรเก็บตั้งแต่ต้น

ไม่ต้อง analytics platform

เขียน summary JSON ก็พอ

ต่อ simulation:

```text
events_per_npc
memories_per_npc
direct_vs_social_memory
average_suspicion_targets
suspicion_changes
false_positive_rate
pattern_detection_count
goal_switch_count
idle_time
schedule_completion_rate
```

เมื่อ Player AI เข้ามาเพิ่ม:

```text
time_to_first_suspicion
number_of_plausible_suspects
correct_top_suspect_rate
player_ai_detection_rate
normal_npc_false_positive_rate
```

เป้าหมายในอนาคต:

```text
หนึ่งรอบควรมี plausible suspects 2–3 คน
```

ไม่ใช่ 1 คนชัดเจน และไม่ใช่ทุกคนมั่วหมด

---

# 22. Performance Strategy

ช่วงแรกไม่ optimize ก่อนเห็นปัญหา

Target MVP:

```text
NPC: 6–20
Simulation decision: 2 Hz
Events: หลักร้อยถึงหลักพัน/นาที
Memory/NPC: จำกัด active memory
```

สิ่งที่ควรทำตั้งแต่แรก:

- immutable event
- bounded recent-event windows
- indexed memory by subject
- dirty recalculation ของ suspicion
- logical location
- fixed tick

สิ่งที่ยังไม่ต้องทำ:

- ECS
- multithreaded brain
- native C++
- custom allocator
- distributed simulation
- GPU compute

เมื่อ profiler บอกว่าจำเป็น ค่อย optimize

---

# 23. Memory Retention Strategy

อย่าเก็บทุก memory ตลอดเกม

ใช้สามระดับ:

```text
Recent
Significant
Archived/Summary
```

MVP:

```text
Recent:
เก็บ 100–200 records / NPC

Significant:
memory ที่ salience สูงหรือเกี่ยวข้องกับ active suspicion

Expired:
remove เมื่อ confidence ต่ำกว่า threshold
```

ภายหลังค่อยทำ memory compression

---

# 24. Development Roadmap

## Phase 0 — Bootstrap

สถานะ: เสร็จแล้ว — solution มี Game.Sim, tests, SimRunner และ Godot .NET project
โดย client reference core ในทิศทางเดียว

**เป้าหมาย:** repository และ deterministic core

งาน:

- solution/project structure
- Godot .NET project
- Game.Sim project
- Game.Sim.Tests
- SimRunner
- EntityId / LocationId / SimTime
- seeded RNG
- CI

Definition of Done:

```text
dotnet test ผ่าน
SimRunner รัน deterministic 10,000 ticks ได้
Godot project reference Game.Sim ได้
```

---

## Phase 1 — World + Event

**เป้าหมาย:** ทำ logical simulation ไม่มี NPC intelligence

สร้าง:

- simulation clock
- entities
- logical locations
- action commands
- world event
- event stream
- JSONL log

DoD:

```text
George สามารถย้าย Hallway → Basement
และเกิด immutable EnterLocation event
```

---

## Phase 2 — Perception

สร้าง:

- perception resolver
- visual/audio channels
- confidence
- observation

DoD:

```text
Anna เห็น George
Bob ไม่เห็น
ผลถูกต้องจากตำแหน่ง logical
```

---

## Phase 3 — Memory

สร้าง:

- episodic memory
- social memory
- decay
- root event lineage
- memory indexes

DoD:

```text
Anna จำ event
Anna บอก Bob
Bob มี social memory ที่ trace กลับ event ต้นทางได้
```

---

## Phase 4 — Suspicion

สร้าง:

- suspicion vector
- suspicion rules
- evidence contribution
- recalculation
- explainable score

DoD:

```text
ทุกคะแนน suspicion อธิบายได้ว่ามาจาก memory ไหน
```

---

## Phase 5 — NPC Routine

สถานะ: เสร็จแล้ว — มี automated test จำลอง NPC 6 ตัวครบ 1 วัน
และยืนยันว่า decisions/events ทำซ้ำได้แบบ deterministic

สร้าง:

- schedule
- needs แบบขั้นต่ำ
- utility goal selection
- actions
- role permissions

DoD:

```text
NPC 6 ตัวใช้ชีวิตครบหนึ่งวันโดยไม่มี scripted timeline
```

---

## Phase 6 — Secrets + False Positives

สถานะ: เสร็จแล้ว — theft, secret meeting และ night owl สร้าง event/evidence จริง
โดย suspicion vector แยก Criminality ออกจากพฤติกรรม player-like ได้

เพิ่ม:

- thief
- secret meeting
- night owl
- liar ภายหลัง

DoD:

```text
NPC ปกติสามารถมีพฤติกรรมน่าสงสัย
แต่ suspicion vector แยก crime กับ player-like ได้
```

---

## Phase 7 — Suspicion-driven Behavior

สถานะ: เสร็จแล้ว — goal ทั้ง 5 แบบสร้างจาก suspicion evidence และตำแหน่งล่าสุด
ใน memory ของ NPC โดยไม่อ่านตำแหน่งจริงของเป้าหมายจาก WorldState

เพิ่ม goal:

```text
ObserveTarget
FollowTarget
AskAboutTarget
ShareSuspicion
AvoidTarget
```

DoD:

```text
NPC เปลี่ยนพฤติกรรมเพราะสิ่งที่ตัวเองเชื่อ
ไม่ใช่เพราะ global truth
```

---

## Phase 8 — Behavior Pattern Detector

สถานะ: เสร็จแล้ว — detector ประมวลผล event stream แบบ incremental/deterministic,
emit pattern ต่อเนื่องเพียงครั้งเดียว และเก็บ EventId ต้นเหตุเพื่ออธิบายผลได้

เพิ่ม:

- LootSweep
- RepeatInteraction
- RoleNeglect
- BoundaryTesting

DoD:

```text
pattern ถูก detect จาก event stream
โดยไม่มี knowledge ว่า actor คือ Player หรือไม่
```

---

## Phase 9 — Player AI Prototype

สถานะ: เสร็จแล้ว — ทั้ง 3 archetype ใช้ routine, movement, interaction,
boundary-probe และ pattern pipeline เดียวกับ actor ปกติ โดย event ไม่มี Player flag

สร้าง archetypes แรก:

```text
Explorer
Completionist
Roleplayer
```

ทุก archetype ใช้ action system เดียวกับ NPC ปกติ

ห้ามมี:

```text
if actor.IsPlayerAI
    suspicious = true
```

ระบบต้อง detect จาก behavior เท่านั้น

---

## Phase 10 — Godot Adapter

สถานะ: เสร็จแล้วสำหรับ prototype — Godot 4.7.2 .NET project reference Game.Sim,
มี Hallway/Basement, restricted door และ actor view ที่แปลง logical location เป็น Vector3
จาก event stream โดย Game.Sim ไม่ reference Godot

DoD:

```text
Godot เปิด Main scene ได้
แสดง actor movement จาก deterministic simulation
Core simulation ยังรันและ test ได้โดยไม่เปิด Godot
```

---

## Phase 11 — Developer Debug Visualization

สถานะ: เสร็จแล้วสำหรับ MVP — มี seed/tick/speed, recent events, selected actor,
memory counts, suspicion evidence และ hotkeys interaction/pause/step/speed/inspect/dump/replay

```text
E     interact ผ่าน InteractionCommand
F1    pause/resume
Space step one tick
F2    speed x2
F3    speed x10
Tab   inspect next actor
F4    dump actor state
F5    dump JSONL event trace
R     reset real-time session
```

---

## Phase 12 — Hotel Vertical Slice

สถานะ: เสร็จแล้วสำหรับ interactive prototype visualization — scenario มี actor 6 ตัว,
George ถูกขับด้วย Explorer Player AI ผ่าน action/pattern pipeline จริง และไม่มี Player flag
ใน event ส่วน Anna/Bob ปิด memory/rumor/suspicion/follow feedback loop ครบ

Hotel scene มี 5 locations ส่วน Basement feedback loop ใช้ Lobby/Hallway/Basement
และเชื่อม NPC decision เข้ากับ actor view แล้ว

---

## Phase 13 — Physical Navigation + Restricted Door

สถานะ: เสร็จแล้วสำหรับ prototype — actor ทุกตัวใช้ `NavigationAgent3D` บน
`NavigationRegion3D`, Basement ถูกกั้นด้วยประตูที่มี collision และ access gate,
`BoundaryProbe` หรือปุ่ม `E` สามารถเปิดประตูได้

Adapter แยก location เป็นสองสถานะ:

```text
requested location  = movement intent จาก real-time simulation session
confirmed location  = ตำแหน่งที่ Godot ยืนยันหลัง actor เดินถึงจริง
```

DoD:

```text
NPC ไม่ teleport ระหว่าง Lobby/Basement
NPC ไม่เริ่มเดินเข้า Basement ขณะประตูปิด
HUD แสดง moving จน NavigationAgent3D ถึงปลายทาง
reset ปิดประตูและคืนตำแหน่ง actor ได้
Godot headless smoke test ไม่มี runtime error
```

หมายเหตุ: requested/confirmed location tracker เป็น presentation state ส่วน Phase 14
เพิ่ม live logical world และ core action completion แยกจาก state ชุดนี้แล้ว

---

## Phase 14 — Hotel Topology + Live Action Handshake

สถานะ: เสร็จแล้วสำหรับ prototype — เพิ่ม pure C# `LocationGraph` และ
`LiveMovementCoordinator` พร้อม lifecycle `Requested → Navigating → Completed/Failed`;
Godot ส่ง arrival/path failure acknowledgement กลับมาให้ core และ `MoveEntityActionHandler`
สร้าง `LeaveLocation`/`EnterLocation` หลัง arrival เท่านั้น

- Lobby, Hallway, Kitchen, Room 201 และ Basement มาจาก `hotel-world.json`
- marker, floor, navigation bounds, portal และ restricted door เป็น data-driven
- route planner ใช้ stable breadth-first search และไม่พึ่งพิกัด Godot
- รองรับ access denied, route unavailable, physical path failure, cancel และ replan
- event ID ของ live movement และ input ใช้ deterministic generator ร่วมกัน

DoD:

```text
action หนึ่งรายการมี lifecycle Requested → Navigating → Completed/Failed
logical location เปลี่ยนเพียงครั้งเดียวหลัง arrival acknowledgement
เส้นทางข้ามหลายห้องและประตู replay ซ้ำได้จาก seed เดิม
```

Phase 15 เปลี่ยน Godot client ให้ใช้ coordinator นี้ผ่าน incremental session โดยตรงแล้ว

---

## Phase 15 — Real-time Simulation Session

สถานะ: เสร็จแล้ว — `BasementScenarioSession` รันทีละ tick และให้ NPC Brain
ส่ง movement request เข้า `LiveMovementCoordinator` โดยตรง หาก actor กำลังเดิน
routine จะเพิ่ม needs/time ต่อ แต่ไม่เลือก goal ซ้อน และ observer เช่น BoundaryProbe
หรือ ShareSuspicion จะทำงานเมื่อ arrival acknowledgement สำเร็จเท่านั้น

- `BasementScenario.Run()` ใช้ session เดียวกันในโหมด auto-ack สำหรับ headless regression
- Godot ใช้ external acknowledgement จาก `NavigationAgent3D`
- movement failure ยกเลิก pending decision และ retry ได้โดยไม่เปลี่ยน logical location
- completion timestamp ใช้เวลา arrival จริง ไม่ใช่เวลาที่ request ถูกสร้าง
- pause, step, speed, interaction, trace และ reset ทำงานกับ live session

DoD:

```text
ไม่มี precomputed EnterLocation event สำหรับ movement ที่ Godot กำลังแสดง
NPC decision รอ Completed/Failed ก่อนวางแผน action ถัดไป
headless session และ Godot session ให้ผล logical event stream ตรงกันเมื่อใช้ acknowledgement ชุดเดียวกัน
```

Godot smoke run ปัจจุบันปิด feedback loop ที่ tick 29 ตามเวลาการเดินจริง:

```text
Anna arrives Basement → George arrives/probes Basement
→ Anna returns Lobby/shares information
→ Anna and Bob follow George to Basement
```

---

# 25. Milestone สำคัญที่สุด — The Basement Test

สถานะ: ผ่านแล้ว — automated integration test ปิด feedback loop ครบตั้งแต่ George
เข้า Basement จน Bob ได้ social memory, สงสัย และเลือก Follow โดยไม่มี scripted quest

ก่อนสร้าง hotel จริง ต้องผ่าน scenario นี้:

```text
George ไป Basement
        ↓
Anna เห็น
        ↓
Anna จำ
        ↓
Anna สงสัย
        ↓
Anna บอก Bob
        ↓
Bob จำคำพูด Anna
        ↓
Bob เริ่มสงสัย George
        ↓
Bob เลือก Follow George
        ↓
การ Follow สร้าง event ใหม่
        ↓
simulation feedback loop ต่อเอง
```

ข้อกำหนด:

- ไม่มี hardcoded `BobFollowGeorge()`
- ไม่มี scripted quest
- ไม่มี global suspicion truth
- reproduce ได้จาก seed
- test แบบ headless ได้

ถ้าผ่าน milestone นี้ architecture หลักถือว่าใช้ได้

---

# 26. สิ่งที่ “ห้ามทำ” ในช่วงแรก

## ยังไม่ทำ UI ใหญ่

ยังไม่ต้องมี:

- evidence board
- relationship graph
- suspicion meters
- dialogue wheel ซับซ้อน
- inventory UI
- quest UI

UI dev มีแค่:

- developer console
- basic interact prompt
- clock optional
- debug hotkeys

## ยังไม่ใช้ LLM

LLM จะทำให้:

- deterministic test ยาก
- debug conversation ยาก
- latency/cost เพิ่ม
- scope ใหญ่ขึ้น

dialogue MVP ใช้ template:

```text
"I saw {actor} near {location} around {time}."
```

พอ core loop สนุกแล้วค่อยประเมิน LLM เพื่อ naturalization ไม่ใช่ใช้เป็น source of truth

## ยังไม่ทำ complex AI navigation

NPC brain คิดระดับ:

```text
GoTo(Basement)
```

Godot เป็นคนแก้ physical navigation

logic ไม่ควรสนใจ waypoint ระดับต่ำ

---

# 27. Risks

## Risk 1 — ทุก NPC ดู random

สาเหตุ:
utility noise เยอะเกิน / schedule อ่อนเกิน

แก้:
normal NPC ต้อง predictable ประมาณหนึ่งก่อน

---

## Risk 2 — Player AI จับง่ายเกิน

สาเหตุ:
player behavior แปลกทุก action

แก้:
ใช้ pattern accumulation และ false positives

---

## Risk 3 — Suspicion พุ่งเร็วเกิน

แก้:
ใช้ confidence, evidence decay, corroboration และ personality

---

## Risk 4 — Debug ไม่รู้สาเหตุ

แก้ตั้งแต่แรกด้วย:

- event id
- memory id
- root event id
- evidence contribution
- seed
- structured log

---

## Risk 5 — Godot กับ core coupling

สัญญาณอันตราย:

```text
Game.Sim import Godot
```

ควรถือว่า architectural violation

dependency ที่ถูกต้อง:

```text
Game.Client.Godot
        ↓
    Game.Sim
```

ไม่ใช่กลับกัน

---

# 28. Coding Conventions ที่แนะนำ

### IDs เป็น value object

```csharp
public readonly record struct EntityId(string Value);
public readonly record struct EventId(Guid Value);
public readonly record struct MemoryId(Guid Value);
public readonly record struct LocationId(string Value);
```

### ใช้ enum สำหรับ closed domain

```csharp
public enum SuspicionDimension
{
    Criminality,
    Secrecy,
    RoleDeviation,
    MetaBehavior,
    ImpossibleBehavior,
    Deception
}
```

### หลีกเลี่ยง global singleton ใน core

dependency ผ่าน constructor

```csharp
public SuspicionSystem(
    IRuleRepository rules,
    ISimRandom random,
    ISimLogger logger)
```

### หลีกเลี่ยง async ใน deterministic loop

async ใช้ได้ใน:

- file IO
- asset loading
- telemetry

แต่ simulation core ใช้ ordered synchronous pipeline

---

# 29. First Implementation Backlog

ลำดับ commit ที่แนะนำ:

```text
01 bootstrap solution
02 add deterministic SimClock
03 add seeded RNG
04 add Entity/Location model
05 add WorldEvent
06 add EventStream
07 add JSONL logger
08 add WorldState
09 add Move/EnterLocation action
10 add PerceptionResolver
11 add Observation
12 add MemoryStore
13 add SocialMemory
14 add RootEvent lineage
15 add SuspicionVector
16 add SuspicionRule
17 add EvidenceContribution
18 implement BasementTest
19 add SimRunner
20 run 1000 deterministic repetitions
```

หลังจากข้อ 20 ค่อยเริ่ม NPC Brain

---

# 30. Definition of Done ของ System Foundation & Post-MVP

Foundation และ Post-MVP Deliverables เสร็จสมบูรณ์แล้ว:

- [x] Core simulation ไม่มี dependency ต่อ Godot
- [x] ทุก random ผ่าน seeded RNG
- [x] Event immutable
- [x] World truth แยกจาก observation
- [x] Observation แยกจาก memory
- [x] Direct memory แยกจาก social memory
- [x] Rumor trace ถึง root event ได้
- [x] Suspicion derived จาก evidence
- [x] ทุก suspicion score explain ได้
- [x] Headless Basement Test ผ่าน
- [x] Automated tests ผ่าน (283/283 tests passed)
- [x] SimRunner รัน 10,000 ticks ได้
- [x] SimRunner รันหลายร้อยรอบได้
- [x] Godot adapter สามารถแสดงผล simulation ได้
- [x] Live movement commit logical location หลัง physical arrival
- [x] Hotel topology/marker/portal/door เป็น data-driven (ขยายเป็น 8 ห้อง)
- [x] NPC Brain รันแบบ incremental และรอ physical acknowledgement
- [x] Godot ไม่ใช้ precomputed movement event เป็นตัวขับ actor แล้ว
- [x] Player Agency & Possess System (`P` Possess, `1-8` Move, `T` Talk, `J` Journal)
- [x] Save-Load Session Snapshot System (100% Deterministic Parity, `F6` Save / `F7` Load)
- [x] Interactive Objects & Clue Discovery (`O` Inspect/Tamper, Safe/Keys/Ledger)
- [x] Dialogue & Clue Inquiry (`Y` Inquire Object / Confront Evidence)
- [x] Reality Anomalies & Meta-Suspicion (SaveReload Déjà Vu & The Blink Fast Travel)
- [x] NPC Coalition & Climax Resolution (`Z` Confess, `X` Deny, `C` Flee)
- [x] Multi-Scenario SimRunner (`basement`, `rumor-cascade`, `deceptive-alibi`, `reality-breach`)
- [x] Godot minimal presentation สำหรับทดสอบ hotel navigation, HUD และ gameplay loop

---

# 31. Recommended Immediate Goal

สถานะ: Roadmap Phase 0–15 และ Post-MVP 1–5 เสร็จสมบูรณ์แล้ว ปัจจุบันอยู่ในช่วง **2D Hybrid First Fun Playtest Development** โดยใช้ 3D prototype เดิมเป็นฐานตรวจ regression เท่านั้น

## 31.1 Locked Game Format

รูปแบบเป้าหมาย:

```text
2D Top-Down Hotel Map
+ Visual Novel Dialogue
+ Detective Journal / Timeline
+ Deterministic Systemic Simulation
= Psychological Social Deduction Mystery
```

ไม่เลือก Pure Visual Novel เพราะลดความสำคัญของ movement/schedule และไม่เลือก Stylized 3D เป็น production direction แรกเพราะเพิ่มภาระด้าน asset, camera, lighting, animation และ performance สำหรับผู้พัฒนาใหม่

Human Player จะควบคุม Host คนเดียวต่อรอบ ปุ่มเปลี่ยนตัวละครอิสระใน 3D prototype ถือเป็น debug feature ส่วน possession ระหว่างรอบจริงจะใช้ได้เมื่อเป็น anomaly ที่มีผลต่อ narrative เท่านั้น

## 31.2 Truth Model

ระบบต้องแยกความจริงสามบทบาท:

```text
Human Host              = ร่างที่มนุษย์ควบคุม
Hidden Player Influence = ตัวละครที่ได้รับ Player AI จาก seed
Incident Culprit         = ผู้ก่อเหตุ Basement ซึ่งอาจไม่ใช่ Player
```

ทั้งสามอาจเป็นคนเดียวกันหรือต่างกันได้ ข้อมูลนี้อยู่ใน `SessionTruth` และห้ามรั่วเข้า `WorldEvent`, Observation, Memory หรือ Suspicion โดยตรง

## 31.3 Revised Roadmap

1. **Milestone 1 — Format & Content Lock (เสร็จแล้ว):** ยืนยันรายชื่อตัวละคร/role, first playable case และ mapping ระหว่างตัวละครใน narrative (`Clara`, `Elias`, `Mira`) กับ internal IDs ปัจจุบัน (`charlie`, `dana`, `evelyn`).
2. **Milestone 2 — 2D Graybox (เสร็จระดับ playable):** แผนที่โรงแรม 8 ห้องแสดงโครงสร้างพื้นที่ ประตู ทางเดินและจุดใช้งาน, character tokens ไม่บังห้อง, click-to-move, clock/schedule และ event feedback ทำงานแล้ว; art ยังเป็น placeholder.
3. **Milestone 3 — Investigation UX (เสร็จระดับ playable vertical slice):** หน้าแรก/ตั้งค่า/onboarding, contextual character actions, Follow, Visual Novel dialogue, inspect, แฟ้มคดีแบบแบ่งหน้า, continuous-time night shift, Insight View, final accusation และ narrative aftermath ใช้งานได้แล้ว; เหลือ external playtest รอบใหม่เพื่อปรับ pacing/wording/เวลา.
4. **Milestone 4 — Deterministic Case Generation (เสร็จแล้ว):** `SessionTruth` / `CaseGenerator` ใช้ PCG32 stream แยก (`RandomSequence = 7717`) จึงไม่เลื่อนลำดับสุ่มของ simulation; seed เดิมได้ case/trace เดิม และ replay ใช้ seed ใหม่จึงได้คดีใหม่ โดย hidden truth ไม่รั่วเข้า WorldEvent/Observation/Memory/Suspicion (บังคับด้วย `SessionTruthIsolationTests`). ยังเปิดทางเลือก `AllowHostAsHiddenPlayer` ไว้ให้ Milestone 5 เปิดตอนมีฉากจบ "You Were The Player".
5. **Milestone 5 — Accusation & Endings (เสร็จแล้ว):** ฉากจบครบสามทางตาม `EndingKind` — Correct Accusation, False Accusation และ You Were The Player — โดยหน้าสรุปคดีเอ่ยชื่อตัวเองได้ และ seed เลือก host เป็น hidden player ได้ราว 1 ใน 6 คืน (`AllowHostAsHiddenPlayer` เปิดแล้ว); เหลือขยายเนื้อหาฉากจบตามผล playtest.
6. **Milestone 6 — First Fun Playtest (เริ่มแล้ว):** เล่นคืนแรกภายในด้วย `--night-report`
ครบ 15 seed และบันทึกผลไว้ที่ `playtest/first-night-findings.md`; ก่อนเชิญผู้ทดสอบภายนอก
ต้องตัดสินใจเรื่องบาลานซ์ที่พบก่อน แล้วจึงทดสอบผู้เล่นใหม่ 3–5 คน วัด onboarding,
suspect diversity, hypothesis changes, false-positive understanding, pacing และ replay intent
โดย `playtest/protocol.md` ยังต้องเพิ่ม metric ของ Exposure / Contradiction / Closing Net
และฉากจบสามทาง ซึ่งตอนนี้ยังไม่มีในตารางวัด.
7. **Milestone 7 — Presentation Polish:** เพิ่ม portraits, room art, sprite animation, anomaly effects และ audio ทีละระบบหลัง gameplay ผ่านเกณฑ์.
8. **Engineering Gate (ผ่านระดับ local/CI):** build 0 warning, tests 283/283 และ headless smoke ไทย/อังกฤษผ่าน; เหลือตรวจ Windows export artifact บนเครื่องแจก build จริง.

ความคืบหน้า Technical/3D Prototype Stabilization:

- [x] เพิ่ม objective/action feedback บน HUD และแก้แถบ Help ให้แสดงจริง
- [x] บังคับลำดับ Coalition Consensus → Confrontation → Climax พร้อมป้องกัน event/ending ซ้ำ
- [x] เพิ่ม snapshot validation และ atomic QuickSave พร้อม error feedback เมื่อไฟล์เสียหรือเข้าถึงไม่ได้
- [x] QuickLoad รักษาสถานะ Interactive Objects, Coalition, Ending และตัวละครที่ Player กำลังควบคุม
- [x] ติดตั้ง Godot 4.7.2 .NET และรัน automated headless playable-loop smoke test ผ่านโดยไม่มี runtime error/resource leak
- [x] เปิด physics interpolation สำหรับจอ refresh rate สูง, reset interpolation หลัง teleport และแสดง FPS/physics rate บน HUD
- [x] ถอด procedural/spatial audio, 3D emotion bubbles และ 3D interactive object nodes ชั่วคราวเพื่อแยกตรวจ frame pacing
- [x] เลือก production presentation เป็น 2D Top-Down + Visual Novel Hybrid
- [x] ล็อก CharacterDefinition และ first playable case แบบ data-driven พร้อม validation
- [x] สร้างฐาน 2D graybox: โรงแรม 8 ห้อง, tokens 6 คน, click-to-move, clock/event feedback และ headless smoke test
- [x] เพิ่ม contextual Talk/Inspect, Visual Novel dialogue และ Detective Journal รุ่นแรก
- [x] เพิ่ม Follow NPC, evidence selection และ Timeline filters (เวลา/ตัวละคร/ห้อง/kind/event type)
- [x] ปรับ dialogue content แบบ data-driven และเพิ่ม readability cues บน 2D HUD
- [x] เพิ่มหน้าแรก/ตั้งค่า/onboarding, ลดคำสั่งหลักเหลือ 3 ปุ่ม และใช้ direct NPC selection จาก feedback 1/5
- [x] เพิ่มกะกลางคืนแบบเวลาเดินต่อเนื่อง, AI shift beats, เหตุการณ์หักมุม, Insight View และตอนจบแพ้–ชนะ
- [x] ปรับ floor plan, token visibility, case-file pagination และฉากจบเชิงเนื้อเรื่องสองช่วง
- [x] ตรวจคำแปลไทยและเพิ่ม regression check ใน Thai smoke test
- [x] ต่อระบบที่เขียนเสร็จแต่ไม่เคยต่อสาย (Schedules, Secrets, Social graph, Anomalies, Needs, Conspiracy)
- [x] เพิ่ม Exposure / Contradiction / Closing Net ให้ลูปการเล่นมีการตัดสินใจ
- [x] เพิ่มฉากจบ You Were The Player ครบ Milestone 5
- [ ] เก็บ external playtest เพื่อปรับ pacing, wording และ contextual action edge cases
- [x] เพิ่ม seed-driven case variation พร้อม same-seed deterministic replay
- [x] เล่นคืนแรกภายในครบ 15 seed ด้วย `--night-report` และบันทึกผลไว้
- [ ] ตัดสินใจเรื่องบาลานซ์จากผลคืนแรก (เกณฑ์ exposure, จังหวะ Closing Net, น้ำหนัก anomaly)
- [ ] เพิ่ม metric ของ Exposure / Contradiction / Closing Net และฉากจบสามทางลงใน playtest protocol
- [ ] เล่น First Fun Playtest และแก้ readability/pacing จากข้อมูลจริง
- [ ] นำ visual/audio กลับแบบวัดประสิทธิภาพทีละระบบ

---

# 32. Final Recommendation

Architecture ที่แนะนำสำหรับโปรเจกต์นี้คือ:

```text
┌────────────────────────────────────┐
│             GODOT 4.7.2            │
│ 2D Hotel Map / Character Tokens    │
│ VN Dialogue / Journal / Accusation │
└──────────────────┬─────────────────┘
                   │ 2D Presentation Adapter
                   ▼
┌────────────────────────────────────┐
│          PURE C# / .NET 8          │
│                                    │
│ Simulation Clock                   │
│ NPC State                          │
│ Utility Brain                      │
│ Event Stream                       │
│ Perception                         │
│ Memory                             │
│ Suspicion                          │
│ Pattern Detection                  │
│ Deterministic RNG                  │
│ SessionTruth / CaseGenerator       │
└──────────────────┬─────────────────┘
                   │
                   ▼
┌────────────────────────────────────┐
│     xUnit + Headless SimRunner     │
│ Scenario Tests / Metrics / Replay  │
└────────────────────────────────────┘

3D hotel/navigation prototype เดิมเก็บไว้เป็น debug/regression scene และไม่ใช่ production presentation หลักใน milestone ปัจจุบัน
```

หลักในการตัดสินใจทุก feature จากนี้ควรถามว่า:

> “Feature นี้ทำให้ผู้เล่นอ่านพฤติกรรม สร้างสมมติฐาน หรือรู้สึกถึงความเสี่ยงจากการสืบได้ดีขึ้นหรือไม่?”

ถ้าไม่ ให้เลื่อนไปก่อน

เป้าหมายของ milestone ถัดไปไม่ใช่เกมที่ดูสวย แต่คือ graybox ที่ผู้เล่นมองเห็นและตีความเหตุการณ์ประเภท:

> “Anna สงสัย George เพราะเห็นเขาลง Basement, บอก Bob, Bob เริ่มตาม George, George จึงเริ่มสงสัย Bob กลับ”

โดยเราไม่ได้ script chain นี้ไว้โดยตรง

เมื่อผู้เล่นใหม่สามารถสร้างและเปลี่ยนสมมติฐานจากเหตุการณ์เหล่านี้ได้ จึงค่อยลงทุนกับ art, animation, anomaly effects และ audio
