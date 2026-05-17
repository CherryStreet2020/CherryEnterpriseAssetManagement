/* ================================================
   CherryAI Product Tour Engine - Comprehensive Edition
   ================================================ */

const CherryTour = (function() {
    let currentStep = 0;
    let tourSteps = [];
    let isActive = false;
    let elements = {};

    const tourDefinitions = {
        'dashboard': [
            {
                target: '.sidebar-brand',
                title: 'CherryAI Enterprise Asset Management',
                content: 'Welcome to your enterprise asset management command center. CherryAI helps you track fixed assets, manage depreciation, schedule maintenance, and run capital projects - all from one integrated platform.',
                position: 'right'
            },
            {
                target: '.stats-grid',
                title: 'Real-Time Portfolio Metrics',
                content: 'Your asset portfolio at a glance: total asset count, gross acquisition value, net book value after depreciation, and year-to-date depreciation expense. These figures update automatically as you add assets and run depreciation.',
                position: 'bottom'
            },
            {
                target: '.stat-card:first-child',
                title: 'Total Assets',
                content: 'Click this card to jump directly to your full asset register. The count includes all active assets across all locations and subsidiaries in your organization.',
                position: 'bottom'
            },
            {
                target: 'a[href="/Assets"]',
                title: 'Asset Register Module',
                content: 'Your complete fixed asset database. Register equipment, vehicles, buildings, and more. Track serial numbers, locations, warranty info, and link assets to cost centers and departments for accurate financial reporting.',
                position: 'right'
            },
            {
                target: 'a[href="/Inventory"]',
                title: 'Physical Inventory & Tracking',
                content: 'Conduct physical inventory counts to verify assets. Create inventory lists, scan barcodes or enter asset tags manually, then reconcile to identify missing or untagged equipment.',
                position: 'right'
            },
            {
                target: 'a[href="/BulkOperations"]',
                title: 'Bulk Operations',
                content: 'Save time with mass updates. Transfer multiple assets between locations, change status for groups of equipment, or perform partial disposals - all in a single operation with full audit trail.',
                position: 'right'
            },
            {
                target: 'a[href="/Maintenance"]',
                title: 'Maintenance Management',
                content: 'Schedule preventive maintenance, track repairs, and manage work orders. Assign technicians, record parts and labor costs, and maintain complete service history for every asset.',
                position: 'right'
            },
            {
                target: 'a[href="/CIP"]',
                title: 'Capital Improvement Project (CIP)',
                content: 'Track capital improvement projects from initial planning through completion. Monitor budgets vs actual spending across 12 cost categories, then capitalize completed projects into fixed assets. Costs accumulate on the balance sheet under the GAAP "Construction in Progress" account until capitalization.',
                position: 'right'
            },
            {
                target: 'a[href="/Depreciation"]',
                title: 'Depreciation Processing',
                content: 'Run monthly or annual depreciation calculations. CherryAI supports multiple methods including Straight-Line, MACRS, Double-Declining Balance, and Canadian CCA classes. Generate journal entries automatically.',
                position: 'right'
            },
            {
                target: 'a[href="/Reports"]',
                title: 'Reports & Analytics',
                content: 'Generate detailed reports: Asset Register, Depreciation Schedules, Maintenance History, CIP Status, and more. Export to Excel or PDF for auditors and management.',
                position: 'right'
            },
            {
                target: 'a[href="/Admin"]',
                title: 'Administration',
                content: 'Configure company settings, manage users and roles, set up GL accounts, departments, locations, and cost centers. Control depreciation defaults and lock accounting periods.',
                position: 'right'
            },
            {
                target: 'a[href="/AI"]',
                title: 'AI Assistant',
                content: 'Ask questions in plain English! "What is our total depreciation this year?" or "Show me assets over $50,000" - the AI queries your data and provides instant answers.',
                position: 'right'
            },
            {
                target: 'a[href="/Help"]',
                title: 'Help Center',
                content: 'Access comprehensive documentation including step-by-step task guides, concept explanations, a searchable glossary, and implementation checklists to get the most out of CherryAI.',
                position: 'right'
            },
            {
                target: '.header-actions',
                title: 'Quick Actions',
                content: 'Access contextual help, start this guided tour anytime, and manage your user account. The Help button opens a quick reference panel with links to relevant documentation.',
                position: 'bottom'
            },
            {
                target: 'button[onclick="startTour()"]',
                title: 'Tour Button',
                content: 'Restart this tour anytime! Each page has its own customized tour highlighting the features most relevant to that section of the application.',
                position: 'bottom'
            }
        ],
        'assets': [
            {
                target: '.page-header',
                title: 'Asset Register',
                content: 'Your central hub for all fixed assets. From here you can view, add, edit, transfer, dispose, and manage the complete lifecycle of every asset in your organization.',
                position: 'bottom'
            },
            {
                target: '.stats-grid',
                title: 'Asset Portfolio Summary',
                content: 'Quick metrics showing active asset count, total acquisition cost, net book value (after accumulated depreciation), and assets by status. Click any card to filter the list below.',
                position: 'bottom'
            },
            {
                target: '.stat-card.stat-success',
                title: 'Active Assets',
                content: 'Assets currently in service and subject to depreciation. Click to filter the list to show only active equipment.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="newAssetModal"]',
                title: 'Register New Asset',
                content: 'Add new equipment to your register. Enter acquisition details, assign to a location and cost center, set up depreciation methods for both GAAP and Tax books, and attach purchase documentation.',
                position: 'left'
            },
            {
                target: 'a[href*="Export"]',
                title: 'Export to Excel',
                content: 'Download your complete asset register or current filtered view to Excel. Perfect for analysis, audits, or sharing with stakeholders who need offline access.',
                position: 'left'
            },
            {
                target: '.data-table thead',
                title: 'Asset List Columns',
                content: 'View key information at a glance: Asset Tag, Description, Location, Acquisition Date, Original Cost, and Net Book Value. Click column headers to sort.',
                position: 'bottom'
            },
            {
                target: '.data-table tbody tr:first-child',
                title: 'Asset Details',
                content: 'Click any row to open the full asset detail page. From there you can view depreciation schedules, maintenance history, attachments, and perform actions like transfers or disposals.',
                position: 'top'
            },
            {
                target: '.clickable-row',
                title: 'Asset Actions Available',
                content: 'On each asset detail page you can: Edit asset information, Transfer to new location, Record capital improvements, Dispose of the asset, Add maintenance records, and Upload attachments.',
                position: 'top'
            }
        ],
        'asset-details': [
            {
                target: '.page-header',
                title: 'Asset Detail View',
                content: 'Complete information for this asset including identification, financial data, depreciation status, and full history of all activities.',
                position: 'bottom'
            },
            {
                target: '.detail-grid',
                title: 'Asset Information',
                content: 'Core asset details: description, model, serial number, manufacturer, location, department, and cost center assignment. All fields are editable with full audit trail.',
                position: 'bottom'
            },
            {
                target: '.depreciation-section',
                title: 'Depreciation Books',
                content: 'View and manage multiple depreciation books (GAAP and Tax). Each book can have different methods, useful lives, and conventions. See accumulated depreciation and remaining book value.',
                position: 'top'
            },
            {
                target: 'button[onclick*="Transfer"]',
                title: 'Transfer Asset',
                content: 'Move this asset to a different location, department, or cost center. Transfers are tracked with effective dates and full audit history.',
                position: 'left'
            },
            {
                target: 'button[onclick*="Dispose"]',
                title: 'Dispose Asset',
                content: 'Record asset retirement, sale, or scrapping. Enter disposal proceeds, and CherryAI calculates gain or loss automatically based on net book value.',
                position: 'left'
            },
            {
                target: 'button[onclick*="Improvement"]',
                title: 'Capital Improvement',
                content: 'Add capital improvements that extend useful life or increase asset value. Improvement costs are capitalized and depreciated according to your policies.',
                position: 'left'
            },
            {
                target: '.maintenance-history',
                title: 'Maintenance History',
                content: 'Complete service record for this asset. View all past maintenance events, scheduled PMs, repairs, and associated costs. Link to work orders for full details.',
                position: 'top'
            },
            {
                target: '.attachments-section',
                title: 'Document Attachments',
                content: 'Store important documents with the asset: purchase orders, invoices, warranties, photos, manuals, and inspection certificates. Categorize attachments for easy retrieval.',
                position: 'top'
            }
        ],
        'maintenance': [
            {
                target: '.page-header',
                title: 'Maintenance Management',
                content: 'Comprehensive maintenance tracking for all your assets. Schedule preventive maintenance, manage work orders, track costs, and maintain complete service history.',
                position: 'bottom'
            },
            {
                target: '.stats-grid',
                title: 'Maintenance Dashboard',
                content: 'At-a-glance metrics: overdue maintenance items requiring immediate attention, upcoming scheduled work, in-progress jobs, and completed maintenance count.',
                position: 'bottom'
            },
            {
                target: '.stat-card.stat-danger',
                title: 'Overdue Maintenance',
                content: 'Critical items past their due date! Click to see all overdue work orders. Addressing these promptly prevents equipment failures and maintains warranty compliance.',
                position: 'bottom'
            },
            {
                target: '.stat-card.stat-warning',
                title: 'Upcoming Maintenance',
                content: 'Scheduled maintenance due within the next 30 days. Plan your technician assignments and parts ordering based on this upcoming workload.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="Modal"]',
                title: 'Create Work Order',
                content: 'Schedule new maintenance: select the asset, choose maintenance type (PM, Repair, Calibration, Inspection), assign a technician, set priority and due date, and add work instructions.',
                position: 'left'
            },
            {
                target: '.data-table',
                title: 'Work Order List',
                content: 'All maintenance records with status, type, assigned technician, and due date. Click any row to view full details, add labor/parts costs, or mark as complete.',
                position: 'top'
            },
            {
                target: '.badge-success',
                title: 'Maintenance Types',
                content: 'CherryAI tracks multiple maintenance types: Preventive Maintenance (PM), Corrective Repair, Calibration, Safety Inspection, Lubrication, and more. Each type can have default intervals and checklists.',
                position: 'left'
            },
            {
                target: 'tr[class*="clickable"]',
                title: 'Work Order Details',
                content: 'Click to open full work order: view work instructions, log labor hours and parts used, add technician notes, upload photos of completed work, and close out the order.',
                position: 'top'
            }
        ],
        'cip': [
            {
                target: '.page-header',
                title: 'Capital Improvement Project',
                content: 'Track capital improvement projects from initial concept through completion and capitalization. Manage budgets, monitor spending by cost category, and convert finished projects into depreciable assets. (Costs accumulate on the balance sheet under the GAAP "Construction in Progress" account until capitalization.)',
                position: 'bottom'
            },
            {
                target: '.stats-grid',
                title: 'Project Portfolio Overview',
                content: 'Summary metrics: total project count, active projects in progress, total budget across all projects, and total spending to date. Monitor budget utilization at a glance.',
                position: 'bottom'
            },
            {
                target: '.stat-card.stat-success',
                title: 'Active Projects',
                content: 'Projects currently underway with costs being accumulated. These projects are not yet capitalized and appear on your balance sheet as Construction in Progress.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="newProjectModal"]',
                title: 'Create New Project',
                content: 'Start a new capital project: enter project details, set the budget, assign a project manager, specify location and department, and establish the target completion date.',
                position: 'left'
            },
            {
                target: '.data-table',
                title: 'Project List',
                content: 'All capital projects with status, budget, spending, and percent complete. Color-coded progress bars show budget utilization - red indicates over-budget projects.',
                position: 'top'
            },
            {
                target: 'tr[class*="clickable"]',
                title: 'Project Details',
                content: 'Click to open project details: add costs across 12 categories (Labor, Materials, Equipment, Permits, Engineering, etc.), upload documents, track milestones, and capitalize when complete.',
                position: 'top'
            },
            {
                target: '.progress-bar',
                title: 'Budget Tracking',
                content: 'Visual indicator of budget consumption. Green means on-track, yellow indicates caution (75-100%), and red shows over-budget. Hover for exact percentages.',
                position: 'left'
            }
        ],
        'cip-details': [
            {
                target: '.page-header',
                title: 'Project Details',
                content: 'Complete project information including budget status, accumulated costs by category, timeline, and all project documentation.',
                position: 'bottom'
            },
            {
                target: '.project-summary',
                title: 'Project Summary',
                content: 'Key project metrics: total budget, amount spent, remaining budget, and completion percentage. Project manager and location assignments.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="AddCost"]',
                title: 'Add Project Cost',
                content: 'Record project expenditures across 12 cost categories: Direct Labor, Contract Labor, Raw Materials, Equipment Rental, Professional Services, Permits, Travel, and more.',
                position: 'left'
            },
            {
                target: '.cost-breakdown',
                title: 'Cost Breakdown',
                content: 'Detailed breakdown of spending by category. Compare actual costs against budget allocations to identify variances and manage project finances.',
                position: 'top'
            },
            {
                target: 'button[onclick*="Capitalize"]',
                title: 'Capitalize Project',
                content: 'When the project is complete, capitalize it to create a new fixed asset. CherryAI transfers all accumulated costs to the asset and begins depreciation.',
                position: 'left'
            },
            {
                target: '.attachments-section',
                title: 'Project Documents',
                content: 'Store contracts, blueprints, permits, invoices, photos, and other project documentation. All documents are linked to the project for easy retrieval and audit support.',
                position: 'top'
            }
        ],
        'depreciation': [
            {
                target: '.page-header',
                title: 'Depreciation Processing',
                content: 'Calculate and post depreciation for all assets. CherryAI supports multiple depreciation methods and maintains separate books for GAAP and Tax reporting.',
                position: 'bottom'
            },
            {
                target: '.depreciation-methods',
                title: 'Depreciation Methods',
                content: 'Choose from industry-standard methods: Straight-Line, Double-Declining Balance, Sum-of-Years-Digits, Units of Production, MACRS (US Tax), and Canadian CCA classes.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="RunDepreciation"]',
                title: 'Run Depreciation',
                content: 'Calculate depreciation for a selected period. Choose the book (GAAP or Tax), fiscal period, and CherryAI calculates depreciation for all eligible assets automatically.',
                position: 'left'
            },
            {
                target: '.depreciation-preview',
                title: 'Depreciation Preview',
                content: 'Review calculated depreciation before posting. See each asset, its depreciation amount, and running totals. Make adjustments if needed before finalizing.',
                position: 'top'
            },
            {
                target: 'button[onclick*="PostJournals"]',
                title: 'Post Journal Entries',
                content: 'Generate journal entries for depreciation expense. Entries are grouped by department and cost center for accurate financial reporting and GL integration.',
                position: 'left'
            },
            {
                target: '.section-179',
                title: 'Section 179 & Bonus Depreciation',
                content: 'US Tax features: Apply Section 179 expensing for immediate deduction, or Bonus Depreciation for accelerated first-year write-offs. Configure per-asset or use defaults.',
                position: 'top'
            }
        ],
        'reports': [
            {
                target: '.page-header',
                title: 'Reports Center',
                content: 'Generate comprehensive reports for assets, depreciation, maintenance, and projects. All reports can be exported to Excel or PDF for distribution and audit support.',
                position: 'bottom'
            },
            {
                target: '.report-category:first-child',
                title: 'Asset Reports',
                content: 'Asset Register (complete listing), Assets by Location, Assets by Category, Assets by Department, Acquisition Report, and Asset Valuation Summary.',
                position: 'bottom'
            },
            {
                target: '.report-category:nth-child(2)',
                title: 'Depreciation Reports',
                content: 'Depreciation Schedule, Depreciation Forecast, Book vs Tax Comparison, Fully Depreciated Assets, and Depreciation Journal Entries.',
                position: 'bottom'
            },
            {
                target: '.report-category:nth-child(3)',
                title: 'Maintenance Reports',
                content: 'Maintenance History, Upcoming PM Schedule, Maintenance Costs by Asset, Technician Workload, and Overdue Maintenance Summary.',
                position: 'bottom'
            },
            {
                target: '.report-category:nth-child(4)',
                title: 'CIP Reports',
                content: 'Project Status Summary, Budget vs Actual Analysis, Cost by Category, and Capitalization Report for completed projects.',
                position: 'bottom'
            },
            {
                target: '.export-options',
                title: 'Export Options',
                content: 'Export any report to Excel (.xlsx) for further analysis or PDF for professional distribution. Schedule automated report delivery via email (coming soon).',
                position: 'left'
            }
        ],
        'admin': [
            {
                target: '.page-header',
                title: 'Administration Center',
                content: 'Configure CherryAI for your organization. Manage users, set up your chart of accounts, define locations and departments, and control system settings.',
                position: 'bottom'
            },
            {
                target: 'a[href*="Company"]',
                title: 'Company Settings',
                content: 'Configure company information, tax registration numbers, fiscal year settings, and choose between Single Company or Multi-Company (Holding) structure.',
                position: 'right'
            },
            {
                target: 'a[href*="Users"]',
                title: 'User Management',
                content: 'Add, edit, and manage user accounts. Assign roles (Admin, Accountant, Viewer) to control access levels. Admins have full access, Accountants can process transactions, Viewers have read-only access.',
                position: 'right'
            },
            {
                target: 'a[href*="GlAccounts"]',
                title: 'Master Files - GL Accounts',
                content: 'Define your GL accounts for asset categories, accumulated depreciation, depreciation expense, gain/loss on disposal, and maintenance costs. Map accounts to asset categories for automatic journal entries.',
                position: 'right'
            },
            {
                target: 'a[href*="Locations"]',
                title: 'Locations',
                content: 'Set up your facility hierarchy: plants, buildings, floors, and areas. Locations are used for asset tracking, maintenance scheduling, and physical inventory.',
                position: 'right'
            },
            {
                target: 'a[href*="Departments"]',
                title: 'Departments',
                content: 'Define organizational departments for cost allocation. Link departments to GL accounts for accurate expense reporting by business unit.',
                position: 'right'
            },
            {
                target: 'a[href*="CostCenters"]',
                title: 'Cost Centers',
                content: 'Create a cost center hierarchy for detailed financial tracking. Cost centers can represent divisions, product lines, or any other cost allocation dimension.',
                position: 'right'
            },
            {
                target: 'a[href*="Technicians"]',
                title: 'Technicians',
                content: 'Manage your maintenance technicians. Set hourly rates, specialties, and certifications. Assign technicians to work orders and track labor costs.',
                position: 'right'
            },
            {
                target: 'a[href*="ProjectManagers"]',
                title: 'Project Managers',
                content: 'Define project managers who can be assigned to CIP projects. Track project assignments and responsibilities.',
                position: 'right'
            },
            {
                target: 'a[href*="AuditLog"]',
                title: 'Audit Log',
                content: 'View complete audit trail of all system activities. See who made changes, what was changed, and when. Filter by user, date range, or entity type.',
                position: 'right'
            },
            {
                target: 'a[href*="SystemSettings"]',
                title: 'System Settings',
                content: 'Configure depreciation defaults, period locking, and other system-wide settings. Lock closed periods to prevent unauthorized changes.',
                position: 'right'
            }
        ],
        'inventory': [
            {
                target: '.page-header',
                title: 'Physical Inventory',
                content: 'Conduct physical counts to verify asset existence and location. Create inventory lists, scan assets, and reconcile discrepancies between book and physical records.',
                position: 'bottom'
            },
            {
                target: '.stats-grid',
                title: 'Inventory Status',
                content: 'Track inventory progress: lists created, assets scanned, scan completion percentage, and discrepancies found. Monitor your inventory count in real-time.',
                position: 'bottom'
            },
            {
                target: 'button[onclick*="CreateList"]',
                title: 'Create Inventory List',
                content: 'Start a new inventory count. Select a location or department, and CherryAI generates a list of expected assets. Print count sheets or use mobile scanning.',
                position: 'left'
            },
            {
                target: '.data-table',
                title: 'Inventory Lists',
                content: 'All inventory lists with status, location, scan progress, and discrepancy count. Click to open a list and begin or continue scanning.',
                position: 'top'
            },
            {
                target: 'tr[class*="clickable"]',
                title: 'Scan & Reconcile',
                content: 'Open a list to scan assets. Mark items as found, note condition, and flag discrepancies. Reconcile missing assets and investigate untagged equipment.',
                position: 'top'
            }
        ],
        'ai': [
            {
                target: '.page-header',
                title: 'AI Assistant',
                content: 'Your intelligent helper for CherryAI. Ask questions in plain English and get instant answers from your asset data. No query language needed!',
                position: 'bottom'
            },
            {
                target: '.chat-input',
                title: 'Ask Anything',
                content: 'Type your question naturally: "What is our total asset value?" "Show me overdue maintenance" "Which projects are over budget?" The AI understands context and queries your data.',
                position: 'top'
            },
            {
                target: '.sample-questions',
                title: 'Example Questions',
                content: 'Try these: "What assets are in Building A?" "Calculate depreciation for 2025" "List top 10 assets by value" "Show maintenance costs last quarter"',
                position: 'bottom'
            },
            {
                target: '.chat-response',
                title: 'Intelligent Responses',
                content: 'The AI provides formatted answers with relevant data, totals, and links to detailed records. Ask follow-up questions to drill deeper into any topic.',
                position: 'top'
            }
        ],
        'help': [
            {
                target: '.page-header',
                title: 'Help Center',
                content: 'Comprehensive documentation for CherryAI. Find step-by-step guides, concept explanations, and implementation checklists to master every feature.',
                position: 'bottom'
            },
            {
                target: '.help-section:first-child',
                title: 'Task Guides',
                content: 'Step-by-step instructions for common tasks: adding assets, running depreciation, scheduling maintenance, creating CIP projects, and more. Each guide walks you through the process.',
                position: 'bottom'
            },
            {
                target: '.help-section:nth-child(2)',
                title: 'Concept Topics',
                content: 'Learn the fundamentals: depreciation methods, tax books, maintenance types, CIP accounting, and more. Understand the "why" behind the features.',
                position: 'bottom'
            },
            {
                target: '.help-section:nth-child(3)',
                title: 'Implementation Guide',
                content: 'Setting up CherryAI for your organization? Follow our phased implementation guide from initial planning through go-live. Includes checklists and best practices.',
                position: 'bottom'
            },
            {
                target: '.glossary-link',
                title: 'Glossary',
                content: 'Not sure what a term means? Our glossary defines MACRS, CCA, CIP, NBV, and dozens of other asset management terms in plain language.',
                position: 'left'
            }
        ],
        'default': [
            {
                target: '.page-header',
                title: 'Current Page',
                content: 'This header shows your current location in CherryAI. Use the breadcrumb navigation above to move back through the page hierarchy.',
                position: 'bottom'
            },
            {
                target: '.breadcrumb',
                title: 'Breadcrumb Navigation',
                content: 'Click any link in the breadcrumb trail to jump back to that level. The breadcrumb shows the full path from Dashboard to your current page.',
                position: 'bottom'
            },
            {
                target: '.sidebar',
                title: 'Main Navigation',
                content: 'Access all CherryAI modules from the sidebar. Your current section is highlighted. Sections are organized by function: Assets, Operations, Finance, and Administration.',
                position: 'right'
            },
            {
                target: '.header-actions',
                title: 'Quick Actions',
                content: 'Access Help for documentation, start a guided Tour of the current page, and manage your user account. Available on every page.',
                position: 'bottom'
            },
            {
                target: 'button[onclick="startTour()"]',
                title: 'Restart Tour',
                content: 'Click Tour anytime to restart the guided walkthrough. Each page has its own customized tour highlighting relevant features and actions.',
                position: 'bottom'
            }
        ]
    };

    function getCurrentPage() {
        const path = window.location.pathname.toLowerCase();
        if (path === '/' || path === '/index') return 'dashboard';
        if (path.includes('/assets/details')) return 'asset-details';
        if (path.includes('/assets')) return 'assets';
        if (path.includes('/maintenance')) return 'maintenance';
        if (path.includes('/cip/details')) return 'cip-details';
        if (path.includes('/cip')) return 'cip';
        if (path.includes('/depreciation')) return 'depreciation';
        if (path.includes('/reports')) return 'reports';
        if (path.includes('/admin')) return 'admin';
        if (path.includes('/inventory')) return 'inventory';
        if (path.includes('/ai')) return 'ai';
        if (path.includes('/help')) return 'help';
        return 'default';
    }

    function createElements() {
        if (document.getElementById('tour-overlay')) return;

        const overlay = document.createElement('div');
        overlay.id = 'tour-overlay';
        overlay.className = 'tour-overlay';
        document.body.appendChild(overlay);

        const highlight = document.createElement('div');
        highlight.id = 'tour-highlight';
        highlight.className = 'tour-highlight';
        document.body.appendChild(highlight);

        const tooltip = document.createElement('div');
        tooltip.id = 'tour-tooltip';
        tooltip.className = 'tour-tooltip';
        tooltip.innerHTML = `
            <div class="tour-tooltip-header">
                <div class="tour-tooltip-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                </div>
                <div class="tour-tooltip-title">
                    <h4 id="tour-title">Title</h4>
                    <span id="tour-step-info">Step 1 of 5</span>
                </div>
                <button class="tour-close" onclick="CherryTour.end()" title="End tour">&times;</button>
            </div>
            <div class="tour-tooltip-content">
                <p id="tour-content">Content</p>
            </div>
            <div class="tour-tooltip-footer">
                <div class="tour-progress" id="tour-progress"></div>
                <div class="tour-nav">
                    <button class="tour-btn tour-btn-secondary" id="tour-prev" onclick="CherryTour.prev()">Back</button>
                    <button class="tour-btn tour-btn-primary" id="tour-next" onclick="CherryTour.next()">Next</button>
                </div>
            </div>
        `;
        document.body.appendChild(tooltip);

        elements = {
            overlay: overlay,
            highlight: highlight,
            tooltip: tooltip
        };
    }

    function showWelcome(callback) {
        const pageName = getCurrentPage();
        const pageTitle = {
            'dashboard': 'Dashboard',
            'assets': 'Asset Register',
            'asset-details': 'Asset Details',
            'maintenance': 'Maintenance',
            'cip': 'Capital Projects',
            'cip-details': 'Project Details',
            'depreciation': 'Depreciation',
            'reports': 'Reports',
            'admin': 'Administration',
            'inventory': 'Inventory',
            'ai': 'AI Assistant',
            'help': 'Help Center',
            'default': 'This Page'
        }[pageName] || 'This Page';

        const stepCount = (tourDefinitions[pageName] || tourDefinitions['default']).length;

        const welcome = document.createElement('div');
        welcome.id = 'tour-welcome';
        welcome.className = 'tour-welcome';
        welcome.innerHTML = `
            <div class="tour-welcome-header">
                <div class="tour-welcome-logo">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z" />
                    </svg>
                </div>
                <h2>CherryAI Tour</h2>
                <p>${pageTitle}</p>
            </div>
            <div class="tour-welcome-content">
                <ul class="tour-feature-list">
                    <li>
                        <div class="tour-feature-icon blue">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5M7.188 2.239l.777 2.897M5.136 7.965l-2.898-.777M13.95 4.05l-2.122 2.122m-5.657 5.656l-2.12 2.122" />
                            </svg>
                        </div>
                        <div class="tour-feature-text">
                            <h5>Interactive Walkthrough</h5>
                            <p>${stepCount} steps covering key features on this page</p>
                        </div>
                    </li>
                    <li>
                        <div class="tour-feature-icon green">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                        <div class="tour-feature-text">
                            <h5>Highlighted Elements</h5>
                            <p>Each feature is highlighted as we explain it</p>
                        </div>
                    </li>
                    <li>
                        <div class="tour-feature-icon purple">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                        <div class="tour-feature-text">
                            <h5>Self-Paced Learning</h5>
                            <p>Navigate back and forth at your own pace</p>
                        </div>
                    </li>
                    <li>
                        <div class="tour-feature-icon amber">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                        <div class="tour-feature-text">
                            <h5>Contextual Help</h5>
                            <p>Learn exactly what's relevant on this page</p>
                        </div>
                    </li>
                </ul>
            </div>
            <div class="tour-welcome-footer">
                <button class="tour-btn tour-btn-primary" id="start-tour-btn">Start Tour</button>
                <span class="tour-skip-link" id="skip-tour-btn">Skip for now</span>
            </div>
        `;
        document.body.appendChild(welcome);

        elements.overlay.classList.add('active');
        setTimeout(() => welcome.classList.add('active'), 50);

        document.getElementById('start-tour-btn').onclick = function() {
            welcome.classList.remove('active');
            setTimeout(() => {
                welcome.remove();
                callback();
            }, 300);
        };

        document.getElementById('skip-tour-btn').onclick = function() {
            welcome.classList.remove('active');
            elements.overlay.classList.remove('active');
            setTimeout(() => welcome.remove(), 300);
            isActive = false;
        };
    }

    function showComplete() {
        const pageName = getCurrentPage();
        const pageTitle = {
            'dashboard': 'Dashboard',
            'assets': 'Asset Register',
            'asset-details': 'Asset Details',
            'maintenance': 'Maintenance',
            'cip': 'Capital Projects',
            'cip-details': 'Project Details',
            'depreciation': 'Depreciation',
            'reports': 'Reports',
            'admin': 'Administration',
            'inventory': 'Inventory',
            'ai': 'AI Assistant',
            'help': 'Help Center',
            'default': 'this page'
        }[pageName] || 'this page';

        const complete = document.createElement('div');
        complete.id = 'tour-complete';
        complete.className = 'tour-complete';
        complete.innerHTML = `
            <div class="tour-complete-icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
            </div>
            <h3>Tour Complete!</h3>
            <p>You've completed the ${pageTitle} tour. Explore the features or visit the Help Center for detailed documentation.</p>
            <div class="tour-complete-actions">
                <button class="tour-btn tour-btn-primary" onclick="CherryTour.closeComplete()">Start Exploring</button>
                <button class="tour-btn tour-btn-secondary" onclick="window.location.href='/Help'">View Help Center</button>
            </div>
        `;
        document.body.appendChild(complete);
        setTimeout(() => complete.classList.add('active'), 50);
    }

    function positionTooltip(targetRect, position) {
        const tooltip = elements.tooltip;
        const tooltipRect = tooltip.getBoundingClientRect();
        const padding = 16;
        let top, left;

        tooltip.className = 'tour-tooltip active';

        switch(position) {
            case 'bottom':
                top = targetRect.bottom + padding;
                left = targetRect.left + (targetRect.width / 2) - (tooltipRect.width / 2);
                tooltip.classList.add('arrow-top');
                break;
            case 'top':
                top = targetRect.top - tooltipRect.height - padding;
                left = targetRect.left + (targetRect.width / 2) - (tooltipRect.width / 2);
                tooltip.classList.add('arrow-bottom');
                break;
            case 'left':
                top = targetRect.top + (targetRect.height / 2) - (tooltipRect.height / 2);
                left = targetRect.left - tooltipRect.width - padding;
                tooltip.classList.add('arrow-right');
                break;
            case 'right':
                top = targetRect.top + (targetRect.height / 2) - (tooltipRect.height / 2);
                left = targetRect.right + padding;
                tooltip.classList.add('arrow-left');
                break;
        }

        left = Math.max(16, Math.min(left, window.innerWidth - tooltipRect.width - 16));
        top = Math.max(16, Math.min(top, window.innerHeight - tooltipRect.height - 16));

        tooltip.style.top = top + 'px';
        tooltip.style.left = left + 'px';
    }

    function showStep(index) {
        const step = tourSteps[index];
        if (!step) return;

        const target = document.querySelector(step.target);
        
        if (!target) {
            if (index < tourSteps.length - 1) {
                currentStep++;
                showStep(currentStep);
            } else {
                end(true);
            }
            return;
        }

        target.scrollIntoView({ behavior: 'smooth', block: 'center' });

        setTimeout(() => {
            const rect = target.getBoundingClientRect();
            
            elements.highlight.style.top = (rect.top - 8) + 'px';
            elements.highlight.style.left = (rect.left - 8) + 'px';
            elements.highlight.style.width = (rect.width + 16) + 'px';
            elements.highlight.style.height = (rect.height + 16) + 'px';
            elements.highlight.style.display = 'block';

            document.getElementById('tour-title').textContent = step.title;
            document.getElementById('tour-content').textContent = step.content;
            document.getElementById('tour-step-info').textContent = `Step ${index + 1} of ${tourSteps.length}`;

            const progress = document.getElementById('tour-progress');
            progress.innerHTML = tourSteps.map((_, i) => 
                `<div class="tour-progress-dot ${i < index ? 'completed' : ''} ${i === index ? 'active' : ''}"></div>`
            ).join('');

            document.getElementById('tour-prev').style.display = index === 0 ? 'none' : 'block';
            
            const nextBtn = document.getElementById('tour-next');
            if (index === tourSteps.length - 1) {
                nextBtn.textContent = 'Finish';
                nextBtn.className = 'tour-btn tour-btn-success';
            } else {
                nextBtn.textContent = 'Next';
                nextBtn.className = 'tour-btn tour-btn-primary';
            }

            positionTooltip(rect, step.position);
            elements.tooltip.classList.add('active');
        }, 300);
    }

    function start() {
        createElements();
        isActive = true;
        currentStep = 0;

        const page = getCurrentPage();
        tourSteps = tourDefinitions[page] || tourDefinitions['default'];

        showWelcome(function() {
            showStep(0);
        });
    }

    function next() {
        if (currentStep < tourSteps.length - 1) {
            currentStep++;
            elements.tooltip.classList.remove('active');
            setTimeout(() => showStep(currentStep), 200);
        } else {
            end(true);
        }
    }

    function prev() {
        if (currentStep > 0) {
            currentStep--;
            elements.tooltip.classList.remove('active');
            setTimeout(() => showStep(currentStep), 200);
        }
    }

    function end(showCompleteModal = false) {
        isActive = false;
        
        if (elements.tooltip) elements.tooltip.classList.remove('active');
        if (elements.highlight) elements.highlight.style.display = 'none';
        
        setTimeout(() => {
            if (elements.overlay) elements.overlay.classList.remove('active');
            if (showCompleteModal) {
                showComplete();
            }
        }, 200);
    }

    function closeComplete() {
        const complete = document.getElementById('tour-complete');
        if (complete) {
            complete.classList.remove('active');
            setTimeout(() => complete.remove(), 300);
        }
    }

    return {
        start: start,
        next: next,
        prev: prev,
        end: end,
        closeComplete: closeComplete,
        isActive: function() { return isActive; }
    };
})();

function startTour() {
    CherryTour.start();
}
