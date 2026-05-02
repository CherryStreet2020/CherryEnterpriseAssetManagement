# CherryAI Agent — Complete Skills Reference

This document catalogs every skill available to the Replit Agent in this workspace. Use this as a prompt reference to know what capabilities are available and when to invoke them.

---

## REPLIT-PROVIDED SKILLS (17 skills)

These are built-in platform capabilities.

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 1 | **agent-inbox** | `.local/skills/agent-inbox` | List and manage user feedback items, bug reports, feature requests from the agent inbox. |
| 2 | **code_review** | `.local/skills/code_review` | Spawn an architect subagent for deep code analysis, planning, and debugging. Call after building major features to evaluate quality. |
| 3 | **database** | `.local/skills/database` | Create/manage Replit PostgreSQL databases, execute SQL queries with safety checks, run read-only queries against production DB. |
| 4 | **delegation** | `.local/skills/delegation` | Delegate tasks to specialized subagents. Synchronous (`subagent`) or async (`startAsyncSubagent`) execution for parallel work. |
| 5 | **deployment** | `.local/skills/deployment` | Configure and publish the project to Replit's cloud hosting. Set deployment settings and suggest publishing when ready. |
| 6 | **diagnostics** | `.local/skills/diagnostics` | Access LSP diagnostics (static errors), suggest project rollback to checkpoints. Use for debugging compile/type errors. |
| 7 | **environment-secrets** | `.local/skills/environment-secrets` | View, set, delete environment variables and secrets. Request secrets from users securely. |
| 8 | **fetch-deployment-logs** | `.local/skills/fetch-deployment-logs` | Fetch and analyze production deployment logs. Debug issues with published/deployed applications. |
| 9 | **integrations** | `.local/skills/integrations` | Search and manage Replit integrations (OAuth, connectors, blueprints). Use for auth, payments, third-party APIs before asking for API keys. |
| 10 | **media-generation** | `.local/skills/media-generation` | Generate AI images, AI videos, and retrieve stock images. Use for all visual content creation. |
| 11 | **package-management** | `.local/skills/package-management` | Install/manage language packages, system dependencies, and programming language runtimes. No Docker/venvs — use this instead. |
| 12 | **repl_setup** | `.local/skills/repl_setup` | Setup web apps in Replit: host config, frontend/backend connectivity, cache control, framework-specific setup (Angular, React, Vite, Vue). |
| 13 | **replit-docs** | `.local/skills/replit-docs` | Search Replit documentation for platform features, pricing, deployments, and capabilities. |
| 14 | **skill-authoring** | `.local/skills/skill-authoring` | Create new reusable skills that extend agent capabilities. Use when the user wants to teach something reusable. |
| 15 | **testing** | `.local/skills/testing` | Run automated Playwright-based UI/e2e tests against the application. Verify features, user flows, forms, visual changes in a real browser. |
| 16 | **web-search** | `.local/skills/web-search` | Search the web and fetch content from URLs. Use for real-time information, API docs, current events. |
| 17 | **workflows** | `.local/skills/workflows` | Manage application workflows: configure, start, stop, restart long-running processes (dev servers, etc.). |

---

## USER-INSTALLED SKILLS (40 skills)

These are custom skills installed into this workspace.

### Development & Architecture

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 1 | **api-design-principles** | `.agents/skills/api-design-principles` | Designing new REST/GraphQL APIs, reviewing API specs, establishing API design standards for intuitive, scalable, maintainable APIs. |
| 2 | **architecture-patterns** | `.agents/skills/architecture-patterns` | Implementing Clean Architecture, Hexagonal Architecture, Domain-Driven Design. Architecting complex backends or refactoring for maintainability. |
| 3 | **modern-javascript-patterns** | `.agents/skills/modern-javascript-patterns` | ES6+ patterns: async/await, destructuring, spread, promises, modules, generators. Refactoring legacy JS or writing clean modern code. |
| 4 | **nodejs-backend-patterns** | `.agents/skills/nodejs-backend-patterns` | Building production-ready Node.js backends with Express/Fastify: middleware, error handling, auth, database integration, microservices. |
| 5 | **nestjs-best-practices** | `.agents/skills/nestjs-best-practices` | Writing/reviewing NestJS code: modules, dependency injection, security, performance patterns. |
| 6 | **mcp-builder** | `.agents/skills/mcp-builder` | Building MCP (Model Context Protocol) servers to let LLMs interact with external services via tools. Python (FastMCP) or Node/TS (MCP SDK). |
| 7 | **context7** | `.agents/skills/context7` | Retrieving up-to-date documentation for any library/framework via Context7 API. Verify correct API usage, find code examples, check current library docs. |

### Frontend & UI/UX

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 8 | **frontend-design** | `.agents/skills/frontend-design` | Creating distinctive, production-grade frontend interfaces. Building web components, pages, dashboards, landing pages. Avoids generic AI aesthetics. |
| 9 | **ui-ux-pro-max** | `.agents/skills/ui-ux-pro-max` | UI/UX design intelligence: 50 styles, 21 palettes, 50 font pairings, 20 chart types, 9 stacks (React, Next, Vue, Svelte, SwiftUI, etc.). Plan/build/review any UI element. |
| 10 | **web-design-guidelines** | `.agents/skills/web-design-guidelines` | Review UI code for Web Interface Guidelines compliance: accessibility, UX audit, design best practices check. |
| 11 | **tailwind-design-system** | `.agents/skills/tailwind-design-system` | Build design systems with Tailwind CSS v4: design tokens, component libraries, responsive patterns. |
| 12 | **canvas-design** | `.agents/skills/canvas-design` | Create visual art in .png/.pdf: posters, designs, static visual pieces using design philosophy. Original visual designs only. |
| 13 | **algorithmic-art** | `.agents/skills/algorithmic-art` | Creating algorithmic/generative art using p5.js: flow fields, particle systems, seeded randomness. Original art only. |
| 14 | **brand-guidelines** | `.agents/skills/brand-guidelines` | Apply Anthropic's official brand colors and typography to artifacts. Use when brand style guidelines or visual formatting applies. |
| 15 | **design-md** | `.agents/skills/design-md` | Analyze projects and synthesize a semantic design system into DESIGN.md files. |

### React & Next.js

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 16 | **vercel-react-best-practices** | `.agents/skills/vercel-react-best-practices` | React/Next.js performance optimization from Vercel Engineering: components, data fetching, bundle optimization. |
| 17 | **vercel-composition-patterns** | `.agents/skills/vercel-composition-patterns` | React composition patterns: compound components, render props, context providers. Refactoring boolean prop proliferation. Includes React 19 APIs. |
| 18 | **next-best-practices** | `.agents/skills/next-best-practices` | Next.js file conventions, RSC boundaries, data patterns, async APIs, metadata, error handling, route handlers, image/font optimization. |
| 19 | **next-upgrade** | `.agents/skills/next-upgrade` | Upgrade Next.js to latest version following official migration guides and codemods. |
| 20 | **react-components** | `.agents/skills/react-components` | Convert designs into modular Vite + React components using AST-based validation. |
| 21 | **remotion-best-practices** | `.agents/skills/remotion-best-practices` | Best practices for Remotion — programmatic video creation in React. |

### Mobile

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 22 | **vercel-react-native-skills** | `.agents/skills/vercel-react-native-skills` | React Native/Expo best practices: performance, animations, native modules, mobile platform APIs. |
| 23 | **swiftui-expert-skill** | `.agents/skills/swiftui-expert-skill` | SwiftUI best practices: state management, view composition, performance, iOS 26+ Liquid Glass. |

### Database

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 24 | **postgresql-table-design** | `.agents/skills/postgresql-table-design` | Design PostgreSQL schemas: data types, indexing, constraints, performance patterns, advanced features. |
| 25 | **supabase-postgres-best-practices** | `.agents/skills/supabase-postgres-best-practices` | Postgres performance optimization from Supabase: query optimization, schema design, database configuration. |

### Authentication

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 26 | **better-auth-best-practices** | `.agents/skills/better-auth-best-practices` | Configure Better Auth: server/client setup, database adapters, sessions, plugins, OAuth, email/password auth in TypeScript. |

### Code Quality & Review

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 27 | **code-reviewer** | `.agents/skills/code-reviewer` | Review code for correctness, maintainability, standards. Supports local changes (staged/working tree) and remote Pull Requests. |
| 28 | **code-review-excellence** | `.agents/skills/code-review-excellence` | Master effective code review practices: constructive feedback, catch bugs early, foster knowledge sharing, maintain team morale. |
| 29 | **receiving-code-review** | `.agents/skills/receiving-code-review` | When receiving code review feedback: requires technical rigor and verification, not blind implementation. |
| 30 | **requesting-code-review** | `.agents/skills/requesting-code-review` | When completing tasks or implementing major features — verify work meets requirements before merging. |
| 31 | **test-driven-development** | `.agents/skills/test-driven-development` | Implementing features/bugfixes: write tests BEFORE implementation code. TDD red-green-refactor cycle. |

### Copywriting & Marketing

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 32 | **copywriting** | `.agents/skills/copywriting` | Write/rewrite/improve marketing copy for any page: homepage, landing, pricing, features, about. Headlines, CTAs, value propositions. |
| 33 | **ab-test-setup** | `.agents/skills/ab-test-setup` | Plan, design, implement A/B tests/experiments. Split tests, multivariate tests, hypothesis formation, statistical significance. |
| 34 | **referral-program** | `.agents/skills/referral-program` | Create/optimize referral programs, affiliate programs, word-of-mouth strategies, ambassador programs, viral loops. |

### Tools & Automation

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 35 | **agent-tools** | `.agents/skills/agent-tools` | Run 150+ AI apps via inference.sh CLI: image generation (FLUX), video (Veo, Seedance), LLMs (Gemini, Grok, Claude), search (Tavily, Exa), 3D, Twitter automation. |
| 36 | **browser-use** | `.agents/skills/browser-use` | Automate browser interactions: web testing, form filling, screenshots, data extraction, web page navigation. |
| 37 | **audit-website** | `.agents/skills/audit-website` | Audit websites for SEO, performance, security, technical issues, content quality using squirrelscan CLI. 230+ rules, health scores, broken links, meta tags. |
| 38 | **find-skills** | `.agents/skills/find-skills` | Discover and install new agent skills. Use when looking for functionality that might exist as an installable skill. |

### Process & Planning

| # | Skill | Path | When to Use |
|---|-------|------|-------------|
| 39 | **brainstorming** | `.agents/skills/brainstorming` | MUST use before any creative work: creating features, building components, adding functionality. Explores intent, requirements, and design before implementation. |
| 40 | **python-performance-optimization** | `.agents/skills/python-performance-optimization` | Profile and optimize Python code: cProfile, memory profilers, performance best practices. Debug slow Python code. |

---

## QUICK TRIGGER REFERENCE

**"I want to build a UI"** → frontend-design, ui-ux-pro-max, web-design-guidelines
**"Review my code"** → code-reviewer, code-review-excellence, code_review (architect)
**"Design a database schema"** → postgresql-table-design, supabase-postgres-best-practices, database
**"Write copy for this page"** → copywriting
**"Set up an A/B test"** → ab-test-setup
**"Audit this website"** → audit-website
**"Find documentation for [library]"** → context7
**"Search the web for..."** → web-search
**"Generate an image"** → media-generation, agent-tools
**"Deploy this app"** → deployment
**"Run tests"** → testing (e2e), test-driven-development (TDD)
**"Set up auth"** → better-auth-best-practices, integrations
**"Create a referral program"** → referral-program
**"Optimize React performance"** → vercel-react-best-practices, vercel-composition-patterns
**"Build a Next.js app"** → next-best-practices, next-upgrade
**"Automate browser tasks"** → browser-use
**"Run AI models"** → agent-tools (inference.sh)
**"Plan before building"** → brainstorming
**"Create a design system"** → tailwind-design-system, design-md
**"Build an MCP server"** → mcp-builder

---

## TOTAL: 57 Skills (17 Replit + 40 User-Installed)
