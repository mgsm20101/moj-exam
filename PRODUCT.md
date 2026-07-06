# Product

## Register

product

## Users

- **Admins** (Ministry of Justice / Judicial Information Center staff): manage topics, the question bank, exam configuration (Topic × Difficulty × Type matrices, grading, lifecycle), monitor live exam sessions, and generate pass/fail reports. Desktop-first, data-dense workflows; need speed and confidence when doing bulk operations (bulk question import, publishing an exam after validation against the live bank).
- **Candidates** (exam takers): register with national ID + phone + name (no persistent account), then sit a timed, auto-graded exam (MCQ / True-False / Fill-in-the-blank) under a batch-gated, queued-entry model. This is a distinct surface from the admin panel with a different personality (calm, low-distraction, exam-pressure-aware) — out of scope for the current admin-panel design work and to be designed separately when that surface is built.

## Product Purpose

An internal government exam-administration platform for the Ministry of Justice's Judicial Information Center. Staff configure and run exams end-to-end — topics → question bank → exam configuration → publish → live monitoring → reporting — while candidates sit exams reliably on modest, free-tier hosting via a batch-gated queue. Success looks like: an admin can build and publish a correct exam quickly and with confidence (the system validates the exam against the live question bank before publish and won't let an under-provisioned exam go live), and the resulting reports are trustworthy enough to base pass/fail decisions on.

## Brand Personality

Modern and professional — official enough to be trusted as a Ministry of Justice system, but not sterile or purely static. Controlled, purposeful motion and micro-interactions are welcome; feedback should feel alive, never like a stiff paper form. Voice: precise and direct, no marketing fluff, no forced friendliness.

## Anti-references

- Not a purely static, robotic "government form" with zero motion or feedback — some tasteful animation is expected and wanted, not just tolerated.
- Not a loud, colorful commercial-SaaS marketing look — no gradient-as-decoration, hero-metric templates, kicker/eyebrow labels, or generic AI-slop scaffolding.
- Not over-designed or whimsical — trust and legibility outrank personality every time they conflict.

## Design Principles

1. **Density with clarity** — admin workflows are dense (question banks, Topic × Difficulty × Type exam matrices); prioritize scanability and hierarchy over decoration.
2. **Validate before it costs you** — constraints like publish-time bank validation (FR-4.9) should surface the exact shortfall inline, at the point of action, not buried in a generic toast.
3. **RTL-first, not RTL-retrofitted** — Arabic is the primary language; layout, icon direction, and motion direction must feel native to RTL, not mirrored as an afterthought over an LTR base.
4. **Confidence through feedback** — every state-changing action (publish / close / archive / delete / save) gets an immediate, motion-backed confirmation; the admin should never wonder whether an action landed.
5. **Official, not sterile** — professional and trustworthy throughout, expressed through controlled, purposeful motion rather than either stiff staticness or playful flourish.

## Accessibility & Inclusion

WCAG 2.1 AA. Full RTL Arabic support is a first-class requirement, not a retrofit over an LTR default. Body text and interactive elements must meet AA contrast ratios. The current font stack (Segoe UI / Tahoma / Arial) is not Arabic-optimized — worth revisiting when DESIGN.md is generated.
