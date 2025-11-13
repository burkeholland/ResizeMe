# PR Implementation Generator - Batch Workflow

## Overview

The updated `pr_implementation_generator.prompt.md` now accepts a **complete master project plan** and generates implementation documentation for **ALL PRs in batch**.

## Input Requirements

**Single Input:** The complete master project plan (output from `project_planning.prompt.md`)

The plan should include:
```markdown
# [Project Name] - Development Plan

## Project Overview
[Description]

## Architecture & Technology Stack
[Tech details and architecture]

## Project Phases & PR Breakdown

### Phase 1: [Phase Name]

#### PR 1.1: [PR Name]
**Branch:** `branch-name`
**Description:** [One sentence]
**Dependencies:** [List or "None"]

#### PR 1.2: [PR Name]
...

### Phase 2: [Phase Name]
...
```

## Output

The generator produces:

1. **Individual PR Documentation Files**
   - One file per PR: `{PR_NUMBER}-{BRANCH_NAME}.md`
   - Example: `1.1-feature-project-setup.md`, `1.2-feature-database-setup.md`
   - Each file is complete and standalone
   - All sections from the template included
   - Copy-paste ready code with no placeholders

2. **Master Implementation Index**
   - Lists all generated PR documents
   - Shows implementation sequence
   - Provides quick navigation

## Workflow

```
Input: Master Project Plan
  ↓
Parse all phases and PRs
  ↓
Research project once (entire tech stack, patterns, docs)
  ↓
Generate PR 1.1 documentation
Generate PR 1.2 documentation
Generate PR 1.3 documentation
... (all PRs)
Generate PR 5.3 documentation
  ↓
Generate Master Implementation Index
  ↓
Output: All PR documentation files + index
```

## Key Features

✅ **Batch Processing** - All PRs generated in one run  
✅ **Consistency** - All PRs use same research context  
✅ **Continuity** - Later PRs properly reference earlier PRs  
✅ **Complete** - No ambiguity, every step is actionable  
✅ **Verified Code** - All code is production-ready  
✅ **Progress Tracking** - Markdown checkboxes throughout  

## Usage Example

### Input to PR Generator:

```
Master Project Plan:

# Task Timer - Development Plan

## Project Overview
A cross-platform desktop task timer with time tracking and reporting.

## Architecture & Technology Stack
- Frontend: Electron + React
- Backend: Node.js + Express
- Database: SQLite
- Deployment: GitHub Releases

## Project Phases & PR Breakdown

### Phase 1: Core Infrastructure

#### PR 1.1: Project Setup & Build Pipeline
**Branch:** `feature/project-setup`
**Description:** Initialize Electron + React project structure
**Dependencies:** None

#### PR 1.2: Database Schema & ORM
**Branch:** `feature/database-setup`
**Description:** Create SQLite schema and data models
**Dependencies:** PR 1.1

#### PR 1.3: Express API Backbone
**Branch:** `feature/api-setup`
**Description:** Set up Express server with task CRUD endpoints
**Dependencies:** PR 1.2

### Phase 2: UI Layer

#### PR 2.1: Main Window & Task List
**Branch:** `feature/task-list-ui`
**Description:** Render task list in React
**Dependencies:** PR 1.1, PR 1.3

...
```

### Output:

```
Generated Files:

1. 1.1-feature-project-setup.md
   - Complete project setup instructions
   - All npm packages listed
   - Step-by-step scaffolding
   - Verification checklist
   - Troubleshooting guide

2. 1.2-feature-database-setup.md
   - Database schema setup
   - Assumes 1.1 is complete
   - All SQL and ORM code
   - Migration setup
   - Testing instructions

3. 1.3-feature-api-setup.md
   - Express server code
   - API endpoint definitions
   - Assumes 1.1 and 1.2 complete
   - Integration with database

2.1-feature-task-list-ui.md
   - React component code
   - UI layout and styling
   - Assumes all Phase 1 PRs complete
   - API integration code

... (all remaining PRs)

IMPLEMENTATION_INDEX.md
   - Complete list of all PR documents
   - Implementation sequence
   - Success criteria
```

## Key Advantages

1. **One Command, All Documentation**
   - No need to run generator multiple times
   - No manual coordination between PR docs
   - Consistent context across all PRs

2. **Automatic Dependency Handling**
   - Generator understands PR dependencies
   - Later PRs reference code from earlier ones correctly
   - Import statements and namespaces align

3. **Context-Aware Code**
   - Code patterns consistent with project
   - Tech stack integrated throughout
   - No generic placeholders

4. **Ready to Implement**
   - Developer follows sequence
   - Checks boxes as they go
   - Each PR is independent and testable

## Integration with Project Planning

**Full Pipeline:**

```
1. Run project_planning.prompt.md
   ↓
   Output: Master project plan (plan.md)

2. User reviews and approves plan

3. Run pr_implementation_generator.prompt.md
   ↓
   Input: Master project plan (plan.md)
   ↓
   Output: All PR documentation (1.1.md, 1.2.md, ... 5.3.md) + index

4. Developers follow PR docs to implement
```

## Customization

The prompt is generic and works with any project type:
- Web applications (React, Vue, Angular)
- Desktop apps (Electron, WinUI, Qt)
- Mobile apps (React Native, Flutter)
- Backend services (Node, Python, Go)
- Infrastructure/DevOps (Terraform, Kubernetes)

Technology stack, code patterns, and APIs are researched automatically for each project.
