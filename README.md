# System-First Development Plan — NPC Reality Deduction Game

> เวอร์ชันเอกสาร: 0.1  
> วันที่: 2026-08-30  
> เป้าหมาย: สร้างเกมต้นแบบที่พิสูจน์ระบบ NPC / Event / Memory / Suspicion ให้สนุกก่อนลงทุนกับ UI, art และ content จำนวนมาก

---

## สถานะการพัฒนาปัจจุบัน

Milestone 0 ถึง The Basement Test เสร็จแล้ว โดยมี Pure C# simulation,
deterministic simulation clock, seeded PCG32 random, strongly typed IDs,
Entity/Location model, WorldState, immutable WorldEvent, deterministic event buffer,
atomic MoveEntity action, deterministic JSONL event logger, xUnit tests,
logical-location topology, visual/audio Observation, Episodic/Social MemoryStore,
RootEventId rumor lineage, confidence decay, data-driven SuspicionRule,
EvidenceContribution, SuspicionVector, daily Schedule, Needs, role permissions,
deterministic Utility-based NPC Brain, Secret Plan สำหรับ theft/secret meeting/night owl,
belief-driven goals สำหรับ observe/follow/ask/share/avoid,
rule-based detector สำหรับ LootSweep/RepeatInteraction/RoleNeglect/BoundaryTesting,
Player AI archetypes แบบ Explorer/Completionist/Roleplayer, headless SimRunner,
information exchange ที่รักษา rumor lineage, autonomous suspicion feedback loop,
Godot 4.7.2 .NET adapter พร้อม Hotel slice 6 actors/5 locations, NavigationAgent3D,
data-driven topology, restricted door, live movement lifecycle,
requested/confirmed location tracking, debug HUD และ GitHub Actions CI

คำสั่งตรวจสอบระบบ:

```bash
dotnet restore Game.sln
dotnet build Game.sln --configuration Release --no-restore
dotnet test Game.sln --configuration Release --no-build
dotnet run --project tools/SimRunner -- --scenario basement --seed 481516 --ticks 16
```

Basement scenario รันผ่าน headless SimRunner พร้อม structured summary, JSONL trace,
metrics และ SHA-256 event fingerprint แล้ว Godot Adapter สามารถแปลง movement replay
เป็น live request และ commit logical location หลัง NavigationAgent3D เดินถึงจริง

เปิด Godot prototype:

```bash
godot --editor --path src/Game.Client.Godot
```

ตรวจ scene แบบ headless เมื่อมี Godot 4.7.2 .NET ใน PATH:

```bash
godot --headless --path src/Game.Client.Godot --quit-after 900
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
**CI:** GitHub Actions สำหรับ `dotnet test`  
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
| CI | GitHub Actions | run test ทุก push/PR |
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
M     move selected actor ไปห้องถัดไปผ่าน live movement lifecycle
F1    pause/resume
Space step one tick
F2    speed x2
F3    speed x10
Tab   inspect next actor
F4    dump actor state
F5    dump JSONL event trace
R     replay
```

---

## Phase 12 — Hotel Vertical Slice

สถานะ: เสร็จแล้วสำหรับ interactive prototype visualization — scenario มี actor 6 ตัว,
George ถูกขับด้วย Explorer Player AI ผ่าน action/pattern pipeline จริง และไม่มี Player flag
ใน event ส่วน Anna/Bob ปิด memory/rumor/suspicion/follow feedback loop ครบ

ขอบเขต slice ปัจจุบันคือ Hallway/Basement สองพื้นที่ และเชื่อม event replay กับ actor view แล้ว

---

## Phase 13 — Physical Navigation + Restricted Door

สถานะ: เสร็จแล้วสำหรับ prototype — actor ทุกตัวใช้ `NavigationAgent3D` บน
`NavigationRegion3D`, Basement ถูกกั้นด้วยประตูที่มี collision และ access gate,
`BoundaryProbe` หรือปุ่ม `E` สามารถเปิดประตูได้

Adapter แยก location เป็นสองสถานะ:

```text
requested location  = ปลายทางจาก deterministic event replay
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

ขอบเขตปัจจุบัน: behavior scenario ยังถูกคำนวณล่วงหน้า แล้ว Godot bridge แปลง
movement event เป็น live request อีกชั้นหนึ่ง ขั้นถัดไปคือให้ simulation session
ตัดสินใจและรอ action completion แบบ real-time โดยไม่ต้อง precompute movement event

---

## Phase 15 — Real-time Simulation Session

สถานะ: ขั้นถัดไป

- เปลี่ยน Basement scenario จาก one-shot `Run()` เป็น session ที่ tick เพิ่มทีละขั้น
- ให้ NPC Brain ส่ง movement request ตรงเข้า `LiveMovementCoordinator`
- actor ที่กำลังเดินต้องมี busy state และไม่เลือก movement goal ซ้อนโดยไม่ตั้งใจ
- ส่ง committed world events เข้า perception/memory/suspicion pipeline ใน tick ถัดไป
- รักษา pause, step, speed, reset และ deterministic replay เดิม

DoD:

```text
ไม่มี precomputed EnterLocation event สำหรับ movement ที่ Godot กำลังแสดง
NPC decision รอ Completed/Failed ก่อนวางแผน action ถัดไป
headless session และ Godot session ให้ผล logical event stream ตรงกันเมื่อใช้ acknowledgement ชุดเดียวกัน
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

# 30. Definition of Done ของ System Foundation

Foundation ถือว่าพร้อมเมื่อ:

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
- [x] Determinism test ผ่าน
- [x] SimRunner รัน 10,000 ticks ได้
- [x] SimRunner รันหลายร้อยรอบได้
- [x] Godot adapter สามารถแสดงผล simulation ได้
- [x] Live movement commit logical location หลัง physical arrival
- [x] Hotel topology/marker/portal/door เป็น data-driven
- [x] ยังไม่มี gameplay system ที่ต้องพึ่ง UI ใหญ่

---

# 31. Recommended Immediate Goal

สถานะ: Hotel Topology + Live Action Handshake เสร็จแล้ว

เป้าหมายถัดไปคือ Phase 15: ทำ real-time simulation session เพื่อให้ NPC Brain
ส่ง action เข้า live movement lifecycle โดยตรง แทนการแปลงจาก precomputed event replay

---

# 32. Final Recommendation

Architecture ที่แนะนำสำหรับโปรเจกต์นี้คือ:

```text
┌────────────────────────────────────┐
│             GODOT 4.7.2            │
│ Rendering / Input / Navigation     │
│ Animation / World Interaction      │
└──────────────────┬─────────────────┘
                   │ Adapter
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
└──────────────────┬─────────────────┘
                   │
                   ▼
┌────────────────────────────────────┐
│     xUnit + Headless SimRunner     │
│ Scenario Tests / Metrics / Replay  │
└────────────────────────────────────┘
```

หลักในการตัดสินใจทุก feature จากนี้ควรถามว่า:

> “Feature นี้ทำให้ simulation สร้างสถานการณ์ที่น่าสนใจขึ้นหรือไม่?”

ถ้าไม่ ให้เลื่อนไปก่อน

เป้าหมายของ milestone แรกไม่ใช่เกมที่ดูดี

แต่คือ simulation ที่เมื่อเปิด log แล้วเราเริ่มเห็นเหตุการณ์ประเภท:

> “Anna สงสัย George เพราะเห็นเขาลง Basement, บอก Bob, Bob เริ่มตาม George, George จึงเริ่มสงสัย Bob กลับ”

โดยเราไม่ได้ script chain นี้ไว้โดยตรง

เมื่อเหตุการณ์แบบนี้เริ่มเกิดขึ้นอย่างสม่ำเสมอ ค่อยขยับไปสร้าง Player AI, gameplay interaction และ presentation layer ต่อ
