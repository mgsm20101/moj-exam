---
name: ExamSystem Admin
description: Ministry of Justice exam-administration console — quiet, RTL-first authority
colors:
  authority-blue: "#1a56db"
  authority-blue-deep: "#153f9e"
  authority-blue-tint: "#e8eefc"
  paper-mist: "#f9fafb"
  pure-white: "#ffffff"
  hairline-grey: "#e5e7eb"
  ink-black: "#111827"
  slate-grey: "#6b7280"
  alert-red: "#ef4444"
  confirm-green: "#10b981"
  caution-amber: "#f59e0b"
typography:
  display:
    fontFamily: "Cairo, \"Segoe UI\", Tahoma, Arial, sans-serif"
    fontSize: "1.75rem"
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: "-0.01em"
  headline:
    fontFamily: "Cairo, \"Segoe UI\", Tahoma, Arial, sans-serif"
    fontSize: "1.375rem"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "normal"
  title:
    fontFamily: "Cairo, \"Segoe UI\", Tahoma, Arial, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "normal"
  body:
    fontFamily: "Cairo, \"Segoe UI\", Tahoma, Arial, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.6
    letterSpacing: "normal"
  label:
    fontFamily: "Cairo, \"Segoe UI\", Tahoma, Arial, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "0.02em"
rounded:
  sm: "4px"
  md: "6px"
  lg: "16px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  2xl: "48px"
components:
  button-primary:
    backgroundColor: "{colors.authority-blue}"
    textColor: "{colors.pure-white}"
    typography: "{typography.body}"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  button-primary-hover:
    backgroundColor: "{colors.authority-blue-deep}"
    textColor: "{colors.pure-white}"
    rounded: "{rounded.md}"
  button-secondary:
    backgroundColor: "{colors.pure-white}"
    textColor: "{colors.authority-blue}"
    typography: "{typography.body}"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  input-default:
    backgroundColor: "{colors.pure-white}"
    textColor: "{colors.ink-black}"
    typography: "{typography.body}"
    rounded: "{rounded.md}"
    padding: "8px 12px"
  card-surface:
    backgroundColor: "{colors.pure-white}"
    rounded: "{rounded.md}"
    padding: "16px"
  table-header:
    backgroundColor: "{colors.paper-mist}"
    textColor: "{colors.slate-grey}"
    typography: "{typography.label}"
    padding: "8px 16px"
---

# Design System: ExamSystem Admin

## 1. Overview

**Creative North Star: "The Official Ledger"**

This is the working console behind a Ministry of Justice exam system: an internal tool for admins who configure exams, curate question banks, and publish with confidence that the numbers add up. The Official Ledger treats every screen like a page in a well-kept register — dense with real data, laid out so the eye finds what it needs in one pass, and quietly authoritative rather than decorated. It is modern and professional, never sterile: motion and feedback are present and controlled, not suppressed in the name of looking "official," and not performed in the name of looking "modern."

The system explicitly rejects the loud, colorful commercial-SaaS marketing look (gradient-as-decoration, hero-metric tiles, kicker/eyebrow labels, generic AI-tool scaffolding) and it equally rejects the opposite failure mode — a purely static, robotic government form with no feedback at all. Every action a admin takes (publish, close, archive, delete, save) earns a clear, immediate, motion-backed acknowledgment. Arabic RTL is the native language of this interface, not a mirrored afterthought: type, iconography, and motion direction are built RTL-first.

**Key Characteristics:**
- Dense, scannable data tables and matrices over decorative cards
- One accent color (Authority Blue), used deliberately and sparingly
- Calm, breathable component spacing even in data-dense screens
- Layered elevation: surfaces read their depth from a persistent, soft shadow, not from borders
- RTL-native layout, motion, and iconography throughout

## 2. Colors

A restrained, single-accent palette: one confident blue carries every primary action and state highlight, everything else is calm neutral or a clearly-scoped status color.

### Primary
- **Authority Blue** (#1a56db): the one color of consequence — primary buttons, active nav state, focus rings, links, and the exam-status "Published" highlight. Reserved for things the admin can act on or should notice first.
- **Authority Blue Deep** (#153f9e): hover/active state for Authority Blue surfaces. Never used at rest.
- **Authority Blue Tint** (#e8eefc): the palest wash of Authority Blue, used only for selected-row backgrounds and focus-ring halos — never as a large flat fill.

### Neutral
- **Paper Mist** (#f9fafb): the app's resting background. Slightly cooler than pure white so white cards read as lifted surfaces against it.
- **Pure White** (#ffffff): card, panel, and table-row background.
- **Hairline Grey** (#e5e7eb): dividers and the 1px rule under table headers. Not used as a card border (see Elevation).
- **Ink Black** (#111827): primary text.
- **Slate Grey** (#6b7280): secondary text, table-header labels, placeholder text, timestamps.

### Status
- **Alert Red** (#ef4444): destructive actions and validation/publish-blocking errors (e.g. the FR-4.9 bank-shortage message).
- **Confirm Green** (#10b981): success states — "Published successfully," active/enabled toggles.
- **Caution Amber** (#f59e0b): warnings that are not yet errors — an exam nearing capacity, a soft validation nudge.

### Named Rules
**The One Ledger Rule.** Authority Blue appears on at most one primary action per view and on active/selected state — never as a decorative background wash, section divider, or illustrative color. Its rarity is what makes it read as "the thing to do here."

## 3. Typography

**Display / Body / Label Font:** Cairo (with "Segoe UI", Tahoma, Arial, sans-serif fallback)

**Character:** A single Arabic-optimized humanist sans, carried across every role by weight and size alone — no serif/sans pairing, no second family. Cairo was chosen over the current bare "Segoe UI, Tahoma, Arial" stack because it is a purpose-built Arabic/Latin typeface with genuinely even Arabic letterforms at both display and body sizes, which the current fallback stack only approximates.

### Hierarchy
- **Display** (600, 1.75rem/28px, line-height 1.25): page-level titles that appear once per screen (e.g. the login card's "تسجيل دخول الأدمن").
- **Headline** (600, 1.375rem/22px, line-height 1.3): section titles (e.g. "إعدادات الامتحانات").
- **Title** (600, 1.125rem/18px, line-height 1.4): panel/card headers nested within a page (e.g. the exam-form panel's "تعديل امتحان").
- **Body** (400, 0.875rem/14px, line-height 1.6): table cells, form inputs and labels, all default running text. Kept at 14px rather than 16px because these screens are data-dense by design (Design Principle: Density with clarity) — 14px is the floor that still clears 4.5:1 contrast comfortably against Pure White and Paper Mist.
- **Label** (600, 0.75rem/12px, letter-spacing 0.02em): table column headers, status badges, small metadata tags.

### Named Rules
**The One-Family Rule.** Every weight and size on screen comes from Cairo. If a second family shows up, it's a bug, not a design choice.

## 4. Elevation

Layered, not flat: cards and panels carry a persistent, soft shadow at rest so they read as lifted surfaces against the Paper Mist background — the shadow is not a hover-only reveal. Because Hairline Grey borders and shadows are never combined on the same surface (see the Named Rule below), the shadow alone is what separates a card from its background.

### Shadow Vocabulary
- **Resting card** (`box-shadow: 0 2px 12px 0 rgba(17, 24, 39, 0.08)`): the default state for every card, panel, and modal-style overlay (e.g. the exam-form panel). Reuses the exact shadow value already shipped in the project's PrimeNG theme import, so adopting it costs nothing new to the bundle.
- **Raised / hover** (`box-shadow: 0 6px 20px 0 rgba(17, 24, 39, 0.12)`): row hover in dense tables, and the momentary state of a card being dragged or actively focused.
- **Overlay** (`box-shadow: 0 16px 40px 0 rgba(17, 24, 39, 0.18)`): the exams-list "form-panel" and any future modal — the one elevation step that should feel like it's floating above the rest of the page.

### Named Rules
**The No-Ghost-Card Rule.** A surface gets a border or a shadow, never both. Cards and panels use the Shadow Vocabulary above with no `border`; only true dividers (a table's header rule, a form's field separator) use Hairline Grey, and those never carry a shadow.

## 5. Components

Buttons, inputs, and cards should all feel **calm and breathable**: generous internal padding, a soft resting shadow instead of a hard outline, transitions on every interactive state so nothing snaps into place.

### Buttons
- **Shape:** 6px radius (`rounded.md`) — matches the radius already defined by the project's PrimeNG import, kept as the one radius value for every interactive control so nothing on screen looks rounder or squarer than its neighbors.
- **Primary:** Authority Blue background, Pure White text, `10px 20px` padding, Body typography at weight 600.
- **Hover / Focus:** background steps to Authority Blue Deep on hover; focus-visible gets a 2px Authority Blue Tint ring offset 2px from the control, transition `background-color 0.15s ease-out, box-shadow 0.15s ease-out`.
- **Secondary / Ghost:** Pure White background, Authority Blue text and 1px Authority Blue border; used for "إغلاق" / cancel-style actions that sit next to a primary button. Reserve plain-text/no-chrome buttons for destructive actions the admin already had to confirm (e.g. row-level حذف).

### Cards / Containers
- **Corner Style:** 6px radius (`rounded.md`).
- **Background:** Pure White on a Paper Mist page background.
- **Shadow Strategy:** Resting-card shadow at rest (see Elevation); Raised shadow only on hover/drag/focus-within.
- **Border:** none — depth comes from the shadow alone (Named Rule: No-Ghost-Card).
- **Internal Padding:** `spacing.md` (16px) minimum; `spacing.lg` (24px) for a page-level panel like the exam-form overlay.

### Inputs / Fields
- **Style:** Pure White background, 1px Hairline Grey border, 6px radius, `8px 12px` padding, Body typography.
- **Focus:** border shifts to Authority Blue, plus the same 2px Authority Blue Tint ring used on buttons — one consistent focus treatment across every control on the site.
- **Error / Disabled:** error state swaps the border to Alert Red and shows the message directly beneath the field in Alert Red, Label typography; disabled drops to Slate Grey text on Paper Mist background with no border.

### Navigation
- **Style:** the admin-layout header is flat (no shadow) with a 1px Hairline Grey rule beneath it, separating it from scrollable content below — the one place a plain border is preferred over a shadow, since the header never needs to look "lifted."
- **Typography:** Body weight 600 for the current nav item, weight 400 for the rest.
- **States:** the active route gets Authority Blue text plus a 2px Authority Blue underline; hover on inactive items shifts text from Slate Grey to Ink Black. RTL-native: nav items read right-to-left in document order, matching Arabic reading direction, not a mirrored LTR list.

### Data Tables (signature component)
Tables are the dominant, most-repeated pattern in this product (question bank, topics, exams) and deserve their own treatment rather than inheriting generic "card" styling.
- **Header row:** Paper Mist background, Slate Grey Label-weight text, 1px Hairline Grey rule beneath — never a shadow.
- **Body rows:** Pure White, 1px Hairline Grey row dividers only (no vertical column rules), Raised-shadow-free at rest.
- **Row hover:** background steps to Authority Blue Tint, signaling "this row is actionable" without needing a hover-only action column to reveal itself.
- **Action cells:** buttons follow the Buttons spec above; destructive actions (حذف) always sit last in reading order (rightmost in the RTL row) so an admin scanning left-to-right through data never lands on "delete" first.

## 6. Do's and Don'ts

### Do:
- **Do** use Cairo as the only type family across the app; vary weight and size, never introduce a second family.
- **Do** give every card, panel, and modal-style overlay the Resting-card shadow (`0 2px 12px 0 rgba(17,24,39,0.08)`) at rest — depth is always visible, not just on hover.
- **Do** confirm every state-changing action (publish / close / archive / delete / save) with an immediate, motion-backed acknowledgment — a toast, an inline status change, or both.
- **Do** build navigation, tables, and motion RTL-first: reading order, icon direction, and animation direction all flow right-to-left as the native case, not as a mirrored LTR default.
- **Do** keep Authority Blue to primary actions, active/selected state, and focus rings only (see the One Ledger Rule).

### Don't:
- **Don't** combine a border and a shadow on the same surface — pick the shadow (Named Rule: No-Ghost-Card).
- **Don't** reach for a loud, colorful commercial-SaaS marketing aesthetic: no gradient-as-decoration, no gradient text, no hero-metric tiles, no tiny uppercase "eyebrow" labels above sections, no generic AI-tool scaffolding.
- **Don't** ship a purely static, feedback-free "government form" screen — motion on interaction is part of the brand, not optional polish.
- **Don't** let PrimeNG's own default theme values (its `--primary-color: #3B82F6` and "Inter var" font, both currently unused anywhere in the codebase) leak into a new component by accident — Authority Blue and Cairo are the only source of truth; if a PrimeNG component is introduced later, re-theme it to match rather than accepting its stock look.
- **Don't** over-design for personality's sake — when trust/legibility and delight conflict, trust and legibility win.
