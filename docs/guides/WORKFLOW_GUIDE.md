# Town Hall Staff Workflow Guide

<!-- AI-ASSISTANT-README -->
This document defines the user workflows for PromptResponse.
Primary audience: Small town hall staff (clerks, administrators).
AI assistants should reference this when designing UI/UX features.
All workflows must be interruptible and forgiving.
<!-- END-AI-ASSISTANT-README -->

## User Profile

### Primary User: Town Hall Clerk

**Name**: Sarah (composite persona)
**Age**: 45
**Tech Comfort**: Moderate (uses email, basic Office)
**Environment**: Front desk, constant interruptions

**Typical Day**:
- 8:00 AM - Open office, check messages
- 8:30 AM - First resident arrives
- Throughout day - Answer phones, help residents, process forms
- 4:00 PM - Process remaining submissions
- 4:30 PM - Prepare for next day

**Pain Points**:
- Interrupted every 5-10 minutes
- Paper forms pile up
- Re-entering data into systems
- Explaining forms to confused residents
- Can't help residents outside office hours

**Goals**:
- Process forms faster
- Fewer errors
- Help residents from home
- Less paper to manage

---

## Core Design Principles

### 1. Interruptible

> "I need to stop what I'm doing and help this person right now."

- **Auto-save everything** - Never lose work
- **Clear resume points** - Know exactly where you left off
- **No long processes** - Break into steps that can pause

### 2. Scannable

> "I have 30 seconds to understand this form."

- **Visual hierarchy** - Most important info first
- **Status indicators** - See state at a glance
- **Clear labels** - No jargon

### 3. Forgiving

> "I clicked the wrong thing, can I undo?"

- **Undo everywhere** - Ctrl+Z works
- **Confirmation for destructive actions** - "Are you sure?"
- **Recovery options** - Nothing is permanent immediately

### 4. Offline-Capable

> "The internet went down, can I still work?"

- **Local-first** - Everything works offline
- **Queue actions** - Sync when connection returns
- **Clear sync status** - Know what's pending

### 5. Simple

> "I don't have time for training."

- **Obvious actions** - Buttons say what they do
- **Progressive disclosure** - Advanced features hidden until needed
- **Contextual help** - Help where you need it

---

## Workflow 1: Creating a New Form Template

### Scenario

The town manager asks Sarah to create a new "Dog License Application" form. Sarah has 20 minutes before her next appointment.

### Step-by-Step Flow

```
┌─────────────────────────────────────────────────┐
│  1. START NEW TEMPLATE                          │
│                                                 │
│  Sarah clicks "New Template" on dashboard       │
│  - Dialog: Template Name, Description           │
│  - Pre-filled: Author (Sarah), Date (today)     │
│  - Action: "Create" button                      │
│                                                 │
│  Time: 30 seconds                               │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  2. ADD SECTIONS                                │
│                                                 │
│  Template editor opens with empty form          │
│  - Left panel: "+ Add Section" button           │
│  - Sarah adds: Owner Info, Pet Info, Vaccines   │
│  - Drag to reorder sections                     │
│                                                 │
│  Time: 2 minutes                                │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  3. ADD FIELDS TO SECTIONS                      │
│                                                 │
│  Sarah clicks section → "+ Add Field"           │
│                                                 │
│  Owner Info:                                    │
│  - Full Name (text, required)                   │
│  - Address (multiline text)                     │
│  - Phone (phone number)                         │
│  - Email (email)                                │
│                                                 │
│  Pet Info:                                      │
│  - Pet Name (text, required)                    │
│  - Breed (text)                                 │
│  - Color (text)                                 │
│  - Spayed/Neutered (yes/no)                     │
│                                                 │
│  Vaccination Records:                           │
│  - Rabies Vaccination Date (date, required)     │
│  - Vet Name (text)                              │
│  - Vet Phone (phone)                            │
│                                                 │
│  Time: 10 minutes                               │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  4. CONFIGURE FIELD PROPERTIES                  │
│                                                 │
│  For each field, Sarah can set:                 │
│  - Label (what the resident sees)               │
│  - Help text (guidance for filling)             │
│  - Required (yes/no)                            │
│  - Expected type (text, number, date, etc.)     │
│                                                 │
│  Example for Rabies Date:                       │
│  - Label: "Rabies Vaccination Date"             │
│  - Help: "From your vet certificate"            │
│  - Required: Yes                                │
│  - Type: Date                                   │
│                                                 │
│  Time: 5 minutes                                │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  5. PREVIEW THE FORM                            │
│                                                 │
│  Sarah clicks "Preview" button                  │
│  - Form displays as resident will see it        │
│  - Can fill in test data                        │
│  - Verify flow makes sense                      │
│                                                 │
│  Time: 2 minutes                                │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  6. SIGN THE TEMPLATE (Optional)                │
│                                                 │
│  Sarah clicks "Sign with Certificate"           │
│  - Select: "Town Clerk Official"                │
│  - Template is now verified/official            │
│  - Residents see "✓ Verified by Town Hall"      │
│                                                 │
│  Time: 30 seconds                               │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  7. PUBLISH TO S3 GALLERY                       │
│                                                 │
│  Sarah clicks "Publish"                         │
│  - Dialog: Choose category (Licenses)           │
│  - Add tags: "pets, dogs, annual, license"      │
│  - Confirm publish                              │
│  - Get shareable link                           │
│                                                 │
│  Time: 1 minute                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  8. SHARE WITH RESIDENTS                        │
│                                                 │
│  Sarah copies the link and:                     │
│  - Sends to webmaster for town website          │
│  - Adds to department email signature           │
│  - Prints on handouts at front desk             │
│                                                 │
│  Done! Form is live.                            │
│                                                 │
│  Total Time: ~20 minutes                        │
└─────────────────────────────────────────────────┘
```

### Interruption Handling

**Scenario**: Phone rings at step 3

1. Sarah answers phone (2 minute call)
2. Returns to PromptResponse
3. Form is exactly where she left it (auto-saved)
4. Status bar shows "Last saved 2 minutes ago"
5. She continues adding fields

**Key Feature**: No "Save" button needed - everything auto-saves

---

## Workflow 2: Resident Filling Out a Form

### Scenario

John needs to register his new dog. He found the form link on the town website and downloaded PromptResponse.

### Step-by-Step Flow

```
┌─────────────────────────────────────────────────┐
│  1. FIND THE FORM                               │
│                                                 │
│  John visits town website                       │
│  - Clicks "Dog License Application" link        │
│  - Opens in PromptResponse (or downloads file)  │
│                                                 │
│  Alternative: Browse gallery in app             │
│  - Open PromptResponse → Browse Gallery         │
│  - Search "dog license"                         │
│  - Click "Download"                             │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  2. VERIFY THE FORM                             │
│                                                 │
│  Form opens in PromptResponse                   │
│  - Banner: "✓ Official Town Hall Form"          │
│  - Signed by: Town Clerk                        │
│  - John knows it's legitimate                   │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  3. FILL OUT THE FORM                           │
│                                                 │
│  Clean, focused interface                       │
│  - One section at a time                        │
│  - Progress bar shows 0% → 100%                 │
│  - Tab between fields                           │
│  - Help text guides each field                  │
│                                                 │
│  Features:                                      │
│  - Auto-save every change                       │
│  - "Save Draft" button for peace of mind        │
│  - "Previous" / "Next" navigation               │
│  - Required fields clearly marked               │
│                                                 │
│  Time: 5-10 minutes                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  4. ADD DIGITAL SIGNATURE                       │
│                                                 │
│  At end of form: "Sign This Form"               │
│                                                 │
│  First time? Create signature:                  │
│  - "Create Your Digital Signature"              │
│  - Enter name: "John Smith"                     │
│  - Click "Create"                               │
│  - Signature saved for future forms             │
│                                                 │
│  Returning? Select existing:                    │
│  - "Sign with: John Smith"                      │
│  - One click to apply                           │
│                                                 │
│  Visual: Seal icon appears on form              │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  5. SUBMIT TO TOWN HALL                         │
│                                                 │
│  John clicks "Submit to Town Hall"              │
│                                                 │
│  Process:                                       │
│  1. Validate all required fields ✓              │
│  2. Apply signature ✓                           │
│  3. Upload to S3 ✓                              │
│  4. Generate confirmation number ✓              │
│                                                 │
│  Result:                                        │
│  - Confirmation: "DL-2024-0001"                 │
│  - "Your form has been submitted"               │
│  - Local copy saved automatically               │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  6. TRACK SUBMISSION                            │
│                                                 │
│  John's dashboard shows:                        │
│  - "Dog License Application"                    │
│  - Status: "Submitted"                          │
│  - Date: "Nov 20, 2024"                         │
│  - Confirmation: "DL-2024-0001"                 │
│                                                 │
│  He can:                                        │
│  - View the submitted form                      │
│  - Print for his records                        │
│  - See it's marked "Submitted"                  │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Offline Scenario

**Scenario**: John starts at home but needs to finish at coffee shop (no wifi)

1. John fills out 70% of form at home
2. Closes laptop, goes to coffee shop
3. Opens PromptResponse - form is there, 70% complete
4. Finishes filling (works offline)
5. Clicks "Submit" - queued for upload
6. Gets wifi - form uploads automatically
7. Receives confirmation

**Key Feature**: Full offline capability with automatic sync

---

## Workflow 3: Town Hall Processing Submissions

### Scenario

It's Monday morning. Sarah has 15 submissions from the weekend to process.

### Step-by-Step Flow

```
┌─────────────────────────────────────────────────┐
│  1. CHECK SUBMISSIONS                           │
│                                                 │
│  Sarah opens PromptResponse                     │
│  - Dashboard shows: "15 new submissions"        │
│  - Badge on "Review Submissions" button         │
│                                                 │
│  She clicks to open submissions view            │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  2. BROWSE SUBMISSIONS                          │
│                                                 │
│  List view with columns:                        │
│  - Form Type (Dog License, Building Permit...)  │
│  - Submitted By (John Smith)                    │
│  - Date (Nov 18, 2024)                          │
│  - Status (New, In Review, Processed)           │
│  - Signature (✓ Valid, ⚠️ Issue)                │
│                                                 │
│  Filters:                                       │
│  - Form type dropdown                           │
│  - Date range                                   │
│  - Status                                       │
│                                                 │
│  Sort:                                          │
│  - Newest first (default)                       │
│  - Oldest first                                 │
│  - By type                                      │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  3. OPEN A SUBMISSION                           │
│                                                 │
│  Sarah double-clicks a submission               │
│                                                 │
│  Form opens with:                               │
│  - All filled data visible                      │
│  - Header: Who submitted, when                  │
│  - Signature status: "✓ Signed by John Smith"   │
│  - Verification: "Signature Valid"              │
│                                                 │
│  Visual: Green banner for valid signature       │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  4. REVIEW AND VERIFY                           │
│                                                 │
│  Sarah checks:                                  │
│  - All required fields filled ✓                 │
│  - Information looks correct ✓                  │
│  - Rabies date is recent ✓                      │
│  - Signature is valid ✓                         │
│                                                 │
│  If issues found:                               │
│  - Click "Request More Info"                    │
│  - Note what's needed                           │
│  - Email sent to resident                       │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  5. TAKE ACTION                                 │
│                                                 │
│  Actions available:                             │
│                                                 │
│  [Approve] - Move to processed                  │
│  [Need Info] - Email resident                   │
│  [Deny] - Archive with reason                   │
│  [Export] - Save as CSV for database            │
│  [Print] - Physical copy if needed              │
│                                                 │
│  Sarah clicks "Approve"                         │
│  - Form moves to "Processed" status             │
│  - Timestamp recorded                           │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  6. BULK OPERATIONS                             │
│                                                 │
│  For routine processing:                        │
│                                                 │
│  Sarah can:                                     │
│  - Select multiple submissions (checkboxes)     │
│  - "Export Selected" → CSV file                 │
│  - "Archive Selected" → Move to archive         │
│                                                 │
│  Export includes all form data for database     │
│  import or reporting                            │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Verification Details

**Signature Verification Display**:

| Status | Icon | Meaning |
|--------|------|---------|
| ✓ Valid | Green check | Signature verified, trusted source |
| ⚠️ Unknown | Yellow warning | Signature valid but signer not trusted |
| ❌ Invalid | Red X | Signature tampered or corrupted |
| ○ Unsigned | Gray circle | Form not signed |

---

## Workflow 4: Setting Up S3 Connection

### Scenario

Sarah is setting up PromptResponse on a new computer. The town already has an S3 bucket configured.

### Step-by-Step Flow

```
┌─────────────────────────────────────────────────┐
│  1. OPEN SETTINGS                               │
│                                                 │
│  Sarah goes to Settings → S3 Configuration      │
│  - Sees "No S3 connection configured"           │
│  - Clicks "Set Up Connection"                   │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  2. ENTER CREDENTIALS                           │
│                                                 │
│  Configuration wizard:                          │
│                                                 │
│  Profile Name: [Town Hall Forms_____]           │
│                                                 │
│  Endpoint URL: [https://s3.amazonaws.com]       │
│    (Dropdown: AWS, MinIO, DigitalOcean, Custom) │
│                                                 │
│  Bucket Name: [townhall-forms_______]           │
│                                                 │
│  Access Key: [AKIA****************]             │
│                                                 │
│  Secret Key: [●●●●●●●●●●●●●●●●●●●●] [👁]        │
│                                                 │
│  Region: [us-east-1_______] (dropdown)          │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  3. TEST CONNECTION                             │
│                                                 │
│  Sarah clicks "Test Connection"                 │
│                                                 │
│  Progress:                                      │
│  - Connecting... ✓                              │
│  - Authenticating... ✓                          │
│  - Checking bucket... ✓                         │
│  - Reading templates... ✓                       │
│  - Checking submissions... ✓                    │
│                                                 │
│  Result: "Connection successful!"               │
│  - Found: 12 templates, 45 submissions          │
│                                                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  4. SAVE PROFILE                                │
│                                                 │
│  Sarah clicks "Save Profile"                    │
│                                                 │
│  Options:                                       │
│  - [✓] Set as default profile                   │
│  - [✓] Remember credentials securely            │
│                                                 │
│  Done! S3 is configured.                        │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Alternative: Import Configuration

If IT provides a config file:

1. Sarah clicks "Import Configuration"
2. Selects `townhall-s3-config.json` from USB
3. Credentials imported
4. Test connection
5. Done in 30 seconds

### CLI Alternative

For IT administrators setting up multiple computers:

```bash
# Set up S3 configuration
promptresponse-cli s3 configure \
  --profile "Town Hall" \
  --endpoint "https://s3.amazonaws.com" \
  --bucket "townhall-forms" \
  --access-key "AKIA..." \
  --secret-key "..." \
  --region "us-east-1"

# Test connection
promptresponse-cli s3 test-connection

# Export for other computers
promptresponse-cli s3 profiles export --output townhall-config.json
```

---

## Workflow 5: Managing Digital Signatures

### Scenario

Sarah needs to create her official Town Clerk signature and add the town's trusted signatures.

### Creating a Signature

```
┌─────────────────────────────────────────────────┐
│  CREATE YOUR SIGNATURE                          │
│                                                 │
│  Think of this as your official stamp           │
│                                                 │
│  Step 1: Your Information                       │
│  ─────────────────────────                      │
│  Your Name: [Sarah Johnson_______]              │
│  Your Role: [Town Clerk__________]              │
│  Organization: [Town of Millbrook]              │
│                                                 │
│  Step 2: Create It                              │
│  ─────────────────                              │
│  This creates a unique digital stamp that       │
│  only you can use to sign documents.            │
│                                                 │
│  [Create My Signature]                          │
│                                                 │
│  Step 3: Keep It Safe                           │
│  ────────────────────                           │
│  ✓ Your signature has been created!             │
│                                                 │
│  Save a backup?                                 │
│  [Save to File] [Save to 1Password] [Skip]      │
│                                                 │
│  Step 4: Ready!                                 │
│  ─────────────                                  │
│  You can now sign documents as:                 │
│  "Sarah Johnson - Town Clerk"                   │
│                                                 │
│  [Start Using My Signature]                     │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Adding Trusted Signatures

```
┌─────────────────────────────────────────────────┐
│  TRUSTED SIGNATURES                             │
│                                                 │
│  Forms from these sources will show as trusted  │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ ✓ State of Vermont                      │   │
│  │   Official state forms                  │   │
│  │   Added: Oct 15, 2024                   │   │
│  │   [Remove]                              │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ ✓ IRS Official Forms                    │   │
│  │   Federal tax forms                     │   │
│  │   Added: Jan 1, 2024                    │   │
│  │   [Remove]                              │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  [+ Add Trusted Source]                         │
│                                                 │
│  How to add:                                    │
│  - Import from file (.cer, .pem)                │
│  - Download from URL                            │
│  - Scan QR code                                 │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Simple Language Guide

| Technical Term | User-Friendly Term |
|----------------|-------------------|
| Certificate | Digital signature / Your stamp |
| Private key | Your secret (never share) |
| Public key | What others see |
| Certificate Authority | Trusted source |
| X.509 | (Never shown) |
| SHA-256 | (Never shown) |
| RSA/ECDSA | (Never shown) |

---

## Error Handling

### Common Errors and Recovery

| Error | Message | Recovery |
|-------|---------|----------|
| No internet | "Can't connect. Working offline." | Queue actions, sync later |
| S3 timeout | "Upload taking longer than usual..." | Retry automatically |
| Invalid signature | "This signature couldn't be verified" | Show details, suggest action |
| Required field missing | "Please fill in: [field name]" | Highlight field, scroll to it |
| File corrupted | "This form appears damaged" | Offer to restore from backup |

### Error Message Guidelines

**Do**:
- Use plain language
- Explain what happened
- Offer next steps
- Keep it brief

**Don't**:
- Show technical errors
- Blame the user
- Use jargon
- Leave user stuck

**Example**:

❌ Bad: "Error 403: Access Denied to s3://bucket/path"

✅ Good: "Can't access the form gallery. Check your S3 settings or contact IT."

---

## Keyboard Shortcuts

### Global

| Shortcut | Action |
|----------|--------|
| Ctrl+N | New template |
| Ctrl+O | Open file |
| Ctrl+S | Save (manual save) |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+P | Print |
| F1 | Help |

### Form Editing

| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+S | Add section |
| Ctrl+Shift+F | Add field |
| Delete | Remove selected |
| Ctrl+↑/↓ | Move item up/down |

### Form Filling

| Shortcut | Action |
|----------|--------|
| Tab | Next field |
| Shift+Tab | Previous field |
| Enter | Submit section / Next |
| Ctrl+Enter | Submit form |

---

## Accessibility Considerations

**Note**: Full accessibility implementation is planned for Month 6. Current workflows should consider:

### Vision

- All text must be readable at 200% zoom
- Color is not the only indicator (use icons + color)
- Screen reader announces all actions

### Motor

- All actions available via keyboard
- Large click targets (44px minimum)
- No time limits on forms

### Cognitive

- Clear, simple language
- Consistent layout across screens
- Progress indicators for multi-step tasks
- Confirmation before destructive actions

---

## Success Metrics

### Staff Efficiency

- Form creation time: < 30 minutes for standard form
- Processing time: < 2 minutes per submission
- Error rate: < 5% of submissions need clarification

### Resident Experience

- Form completion time: < 10 minutes for standard form
- Abandonment rate: < 10%
- First-time success rate: > 90%

### System Reliability

- Upload success rate: > 99%
- Auto-save data loss: 0%
- Offline capability: 100% of core features

---

*Workflow guide version 1.0 - Updated 2024-11-20*
