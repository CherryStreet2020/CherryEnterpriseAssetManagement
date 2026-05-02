using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Help
{
    public class TasksModel : PageModel
    {
        public string Title { get; set; } = "";
        public string ContentHtml { get; set; } = "";
        public Dictionary<string, string> RelatedTasks { get; set; } = new();

        public IActionResult OnGet(string id)
        {
            var task = GetTask(id ?? "");
            if (task == null)
                return RedirectToPage("/Help/Index");

            Title = task.Title;
            ContentHtml = task.Content;
            RelatedTasks = task.RelatedTasks;
            return Page();
        }

        private static TaskGuide? GetTask(string id)
        {
            return id.ToLower() switch
            {
                "view-assets" => new TaskGuide
                {
                    Title = "How to View Your Assets",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Learn how to navigate the asset list, search for specific equipment, filter by status or location, and access detailed asset information.</p>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to the Assets Page</h3>
                                    <p>Click <a href='/Assets' class='task-link'><strong>Assets</strong></a> in the left sidebar under ""Asset Management"". You can also click directly on <a href='/' class='task-link'>Dashboard</a> and then click any asset summary to navigate there.</p>
                                    <div class='step-screenshot'>
                                        You'll see a list of all your company's fixed assets organized in a table with key information visible at a glance.
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Understanding the Asset List</h3>
                                    <p>Each row shows one asset with these columns:</p>
                                    <ul>
                                        <li><strong>Asset #</strong> - Unique identifier for tracking</li>
                                        <li><strong>Description</strong> - What the asset is (e.g., ""CNC Vertical Mill"")</li>
                                        <li><strong>Model</strong> - Manufacturer's model name or number</li>
                                        <li><strong>Location</strong> - Where the asset is physically located</li>
                                        <li><strong>Year</strong> - When it was placed in service</li>
                                        <li><strong>Cost</strong> - Original acquisition/purchase price</li>
                                        <li><strong>Book Value</strong> - Current value after accumulated depreciation</li>
                                        <li><strong>Status</strong> - Active, Disposed, or Inactive</li>
                                    </ul>
                                    
                                    <div class='tip-box'>
                                        <i class='fas fa-lightbulb'></i>
                                        <div class='tip-box-content'>
                                            <p><strong>Tip:</strong> Click on any column header to sort the list. Click again to reverse the sort order.</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Search for a Specific Asset</h3>
                                    <p>Use the <strong>search box</strong> at the top of the page to quickly find assets by:</p>
                                    <ul>
                                        <li>Asset number (exact or partial match)</li>
                                        <li>Description keywords</li>
                                        <li>Location code</li>
                                        <li>Model name or number</li>
                                        <li>Serial number</li>
                                    </ul>
                                    <div class='info-box'>
                                        <i class='fas fa-info-circle'></i>
                                        <div class='info-box-content'>
                                            <p><strong>Pro Tip:</strong> Use the <a href='/AI' class='task-link'>AI Assistant</a> to ask natural language questions like ""Show me all machines in Building A"" or ""What's our most valuable asset?""</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>View Asset Details</h3>
                                    <p>Click on any asset row to open its detailed view. The detail page shows:</p>
                                    <ul>
                                        <li>Complete depreciation history and current book values</li>
                                        <li>Serial number, vendor, and purchase information</li>
                                        <li>CCA tax class assignment (for Canadian tax)</li>
                                        <li>Transfer history (location/department changes)</li>
                                        <li>Capital improvements and cost additions</li>
                                        <li>Maintenance records linked to this asset</li>
                                        <li>Attached documents and photos</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Export Your Asset List</h3>
                                    <p>Need the data in a spreadsheet? Go to <a href='/Reports' class='task-link'><strong>Reports</strong></a> and select ""Asset Register"" to download as Excel or PDF.</p>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=add-asset'>Add a new asset</a> to your register</li>
                                <li><a href='/Help/Tasks?id=dispose-asset'>Dispose an asset</a> you no longer use</li>
                                <li><a href='/Help/Tasks?id=transfer-asset'>Transfer an asset</a> to a different location</li>
                                <li><a href='/Help/Topic?id=assets'>Learn more about asset management concepts</a></li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "add-asset", "Add New Asset" }, { "dispose-asset", "Dispose Asset" }, { "transfer-asset", "Transfer Asset" }, { "export-reports", "Export Reports" } }
                },

                "add-asset" => new TaskGuide
                {
                    Title = "How to Add a New Asset",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Register a new piece of equipment in your asset management system with all the details needed for proper tracking, depreciation, and reporting.</p>
                        </div>

                        <div class='prereq-box'>
                            <h4><i class='fas fa-clipboard-check'></i> Before you begin</h4>
                            <ul>
                                <li>Have the purchase invoice or documentation handy</li>
                                <li>Know the acquisition cost and in-service date</li>
                                <li>Determine the useful life and depreciation method</li>
                                <li>Identify the physical location where the asset is installed</li>
                            </ul>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Add Asset</h3>
                                    <p>Click <a href='/Assets/Create' class='task-link'><strong>+ Add Asset</strong></a> in the left sidebar under ""Asset Management"", or click the ""+ Add New Asset"" button on the <a href='/' class='task-link'>Dashboard</a>.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Enter Basic Information (Required)</h3>
                                    <p>These fields are required to create the asset:</p>
                                    <ul>
                                        <li><strong>Asset Number</strong> - A unique ID for this asset (e.g., ""845"", ""MACH-001"", or ""CNC-2024-01"")</li>
                                        <li><strong>Description</strong> - Clear description of what the asset is (e.g., ""Haas VF-2 CNC Vertical Mill"")</li>
                                        <li><strong>Acquisition Cost</strong> - Total purchase price including installation costs</li>
                                        <li><strong>In-Service Date</strong> - The date you started using the asset (depreciation starts from this date)</li>
                                    </ul>
                                    
                                    <div class='tip-box'>
                                        <i class='fas fa-lightbulb'></i>
                                        <div class='tip-box-content'>
                                            <p><strong>Tip:</strong> Use a consistent numbering system across all assets. Many companies use prefixes by type (e.g., ""MACH-"" for machinery, ""VEH-"" for vehicles).</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Add Identification Details (Optional but Recommended)</h3>
                                    <p>These fields improve tracking and reporting:</p>
                                    <ul>
                                        <li><strong>Model</strong> - Manufacturer's model name/number</li>
                                        <li><strong>Serial Number</strong> - For warranty claims and identification</li>
                                        <li><strong>Manufacturer</strong> - Who made the equipment</li>
                                        <li><strong>Vendor</strong> - Where you purchased it from</li>
                                        <li><strong>Location</strong> - Building or facility code (e.g., ""MISS"", ""BRAM"")</li>
                                        <li><strong>Bay</strong> - Specific area within the location</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Configure Depreciation Settings</h3>
                                    <p>Set how the asset will be depreciated for accounting purposes:</p>
                                    <ul>
                                        <li><strong>Useful Life (Months)</strong> - How long you expect to use it (e.g., 120 months = 10 years)</li>
                                        <li><strong>Salvage Value</strong> - Estimated value at end of useful life (often $0 or 10% of cost)</li>
                                        <li><strong>Depreciation Method</strong> - Usually ""Straight-Line"" for simplicity</li>
                                    </ul>
                                    
                                    <div class='info-box'>
                                        <i class='fas fa-info-circle'></i>
                                        <div class='info-box-content'>
                                            <p><strong>Common useful lives:</strong> Machinery 7-15 years, Vehicles 5-7 years, Computers 3-5 years, Buildings 25-40 years</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Assign Tax Class (For Canadian CCA)</h3>
                                    <p>If you're tracking Canadian taxes, select the appropriate <a href='/CCA' class='task-link'>CCA Class</a>:</p>
                                    <ul>
                                        <li><strong>Class 8 (20%)</strong> - Most manufacturing equipment</li>
                                        <li><strong>Class 10 (30%)</strong> - Vehicles and general-purpose electronic equipment</li>
                                        <li><strong>Class 43 (30%)</strong> - Manufacturing and processing machinery</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>6</div>
                                <div class='step-content'>
                                    <h3>Save the Asset</h3>
                                    <p>Click the <strong>Save</strong> button. The asset will immediately:</p>
                                    <ul>
                                        <li>Appear in your <a href='/Assets' class='task-link'>Assets list</a></li>
                                        <li>Update the <a href='/' class='task-link'>Dashboard</a> totals</li>
                                        <li>Be ready for depreciation calculations</li>
                                        <li>Show in applicable CCA class pools</li>
                                    </ul>
                                    
                                    <div class='success-box'>
                                        <i class='fas fa-check-circle'></i>
                                        <div class='success-box-content'>
                                            <p><strong>Done!</strong> Your asset is now registered and will be included in future depreciation runs.</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=add-attachment'>Attach documents</a> like invoices or manuals to the asset</li>
                                <li><a href='/Help/Tasks?id=schedule-maintenance'>Schedule maintenance</a> for the new equipment</li>
                                <li><a href='/Help/Tasks?id=run-depreciation'>Run depreciation</a> to calculate the first month's expense</li>
                                <li><a href='/Help/Tasks?id=view-assets'>View all your assets</a> to verify it was added correctly</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "add-attachment", "Add Attachments" }, { "schedule-maintenance", "Schedule Maintenance" } }
                },

                "dispose-asset" => new TaskGuide
                {
                    Title = "How to Dispose of an Asset",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #fef3c7; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>When to use:</strong> When you sell, scrap, or retire an asset that you no longer use.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Find the Asset</h3>
                                    <p>Go to <a href='/Assets' class='task-link'><strong>Assets</strong></a> and find the asset you want to dispose.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Open Asset Details</h3>
                                    <p>Click on the asset row to open its detail page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Click Dispose</h3>
                                    <p>Click the <strong>Dispose</strong> button (usually in the actions area).</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Enter Disposal Information</h3>
                                    <ul>
                                        <li><strong>Disposal Date</strong> - When you got rid of it</li>
                                        <li><strong>Disposal Proceeds</strong> - How much you sold it for ($0 if scrapped)</li>
                                        <li><strong>Disposal Reason</strong> - Sold, Scrapped, Donated, etc.</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Review Gain/Loss</h3>
                                    <p>The system calculates:</p>
                                    <div style='background: #f1f5f9; padding: 1rem; border-radius: 8px; margin: 0.5rem 0;'>
                                        <strong>Gain/Loss = Proceeds - Book Value</strong>
                                        <ul style='margin: 0.5rem 0 0 0;'>
                                            <li>Positive = Gain (you sold for more than book value)</li>
                                            <li>Negative = Loss (you sold for less than book value)</li>
                                        </ul>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>6</div>
                                <div class='step-content'>
                                    <h3>Confirm Disposal</h3>
                                    <p>Click <strong>Complete Disposal</strong>. A journal entry is automatically created to record the transaction.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "run-depreciation", "Run Depreciation" } }
                },

                "transfer-asset" => new TaskGuide
                {
                    Title = "How to Transfer an Asset",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #dbeafe; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>When to use:</strong> When you move an asset from one location or department to another.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Find the Asset</h3>
                                    <p>Go to <a href='/Assets' class='task-link'><strong>Assets</strong></a> and click on the asset you want to transfer.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Click Transfer</h3>
                                    <p>Click the <strong>Transfer</strong> button in the asset details page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Enter New Location</h3>
                                    <ul>
                                        <li><strong>To Location</strong> - New building/site (e.g., ""BRAM"")</li>
                                        <li><strong>To Bay</strong> - Specific area (optional)</li>
                                        <li><strong>To Department</strong> - New department (optional)</li>
                                        <li><strong>Transfer Date</strong> - When the move happened</li>
                                        <li><strong>Notes</strong> - Reason for transfer</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Complete Transfer</h3>
                                    <p>Click <strong>Complete Transfer</strong>. The asset's location is updated and the transfer is logged in the history.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "add-improvement", "Add Improvement" } }
                },

                "run-depreciation" => new TaskGuide
                {
                    Title = "How to Run Monthly Depreciation",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #dcfce7; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>What this does:</strong> Calculates depreciation for all active assets and creates journal entries to record it.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Journals</h3>
                                    <p>Click <a href='/Journals' class='task-link'><strong>Journals</strong></a> in the left sidebar under ""Depreciation"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Click Generate Depreciation</h3>
                                    <p>Click the <strong>Generate Depreciation</strong> button at the top of the page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Select the Period</h3>
                                    <ul>
                                        <li><strong>Year</strong> - Select the year (e.g., 2026)</li>
                                        <li><strong>Month</strong> - Select the month to process</li>
                                        <li><strong>Book</strong> - Choose GAAP or Tax book</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Preview the Entries</h3>
                                    <p>The system shows you what entries will be created. Review to make sure they look correct.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Generate Entries</h3>
                                    <p>Click <strong>Generate</strong> to create the journal entries. They'll appear in the journals list.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>6</div>
                                <div class='step-content'>
                                    <h3>View Results</h3>
                                    <p>The new entries appear in the <a href='/Journals' class='task-link'>Journals</a> list. Each shows the total depreciation expense for that period.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-journals", "View Journals" }, { "export-reports", "Export Reports" } }
                },

                "view-journals" => new TaskGuide
                {
                    Title = "How to View Journal Entries",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Journals</h3>
                                    <p>Click <a href='/Journals' class='task-link'><strong>Journals</strong></a> in the left sidebar.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Understanding the List</h3>
                                    <p>Each row shows a journal entry with:</p>
                                    <ul>
                                        <li><strong>Batch</strong> - Identifier for the entry</li>
                                        <li><strong>Date</strong> - When it was posted</li>
                                        <li><strong>Period</strong> - Accounting period (YYYYMM format)</li>
                                        <li><strong>Description</strong> - What the entry is for</li>
                                        <li><strong>Debits/Credits</strong> - Total amounts (should be equal)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Filter by Period or Book</h3>
                                    <p>Use the filter dropdowns at the top to show only:</p>
                                    <ul>
                                        <li>Specific months/years</li>
                                        <li>GAAP or Tax book entries</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>View Entry Details</h3>
                                    <p>Click on any entry to see the full breakdown of debits and credits by account.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "run-depreciation", "Run Depreciation" }, { "export-reports", "Export Reports" } }
                },

                "view-cca" => new TaskGuide
                {
                    Title = "How to View CCA Tax Classes",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to CCA Classes</h3>
                                    <p>Click <a href='/CCA' class='task-link'><strong>CCA Classes</strong></a> in the left sidebar under ""Tax (Canada)"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Understanding CCA Classes</h3>
                                    <p>Each class shows:</p>
                                    <ul>
                                        <li><strong>Class Number</strong> - CRA's class designation (1, 8, 10, 43, etc.)</li>
                                        <li><strong>Rate</strong> - Annual depreciation rate allowed</li>
                                        <li><strong>Description</strong> - What types of assets belong</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>View UCC Schedule</h3>
                                    <p>Click <a href='/CCA/ClassReport' class='task-link'><strong>CCA Schedule</strong></a> to see the UCC rollforward showing:</p>
                                    <ul>
                                        <li>Opening UCC (start of year balance)</li>
                                        <li>Additions (new assets)</li>
                                        <li>Disposals (sold/scrapped assets)</li>
                                        <li>CCA Claimed (depreciation deduction)</li>
                                        <li>Closing UCC (end of year balance)</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "export-reports", "Export Reports" }, { "view-assets", "View Assets" } }
                },

                "export-reports" => new TaskGuide
                {
                    Title = "How to Export Reports",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Reports</h3>
                                    <p>Click <a href='/Reports' class='task-link'><strong>Reports</strong></a> in the left sidebar.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Choose a Report Type</h3>
                                    <p>Select what you want to export:</p>
                                    <ul>
                                        <li><strong>Asset Reports</strong> - Full list of all assets</li>
                                        <li><strong>Journal Reports</strong> - All depreciation entries</li>
                                        <li><strong>CCA Tax Reports</strong> - Canadian tax depreciation by class</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Choose a Format</h3>
                                    <p>Click one of the format buttons:</p>
                                    <ul>
                                        <li><strong>CSV</strong> - Opens in Excel, Google Sheets, or any spreadsheet</li>
                                        <li><strong>Excel</strong> - Formatted spreadsheet with headers</li>
                                        <li><strong>PDF</strong> - Professional report ready to print or email</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Download the File</h3>
                                    <p>The file downloads automatically. Open it or send it to your accountant!</p>
                                </div>
                            </div>
                        </div>
                        
                        <div style='margin-top: 2rem; padding: 1.5rem; background: #f0fdf4; border-radius: 8px;'>
                            <h3 style='margin-top: 0; color: #16a34a;'>Quick Export Links</h3>
                            <p>Download reports right now:</p>
                            <div style='display: flex; gap: 0.5rem; flex-wrap: wrap; margin-top: 1rem;'>
                                <a href='/Reports/Export?type=assets&format=excel' class='btn btn-primary'>Assets (Excel)</a>
                                <a href='/Reports/Export?type=journals&format=excel' class='btn btn-primary'>Journals (Excel)</a>
                                <a href='/Reports/Export?type=assets&format=pdf' class='btn btn-secondary'>Assets (PDF)</a>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-journals", "View Journals" }, { "view-cca", "View CCA Classes" } }
                },

                "view-books" => new TaskGuide
                {
                    Title = "How to View Depreciation Books",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Books</h3>
                                    <p>Click <a href='/Books' class='task-link'><strong>Books</strong></a> in the left sidebar under ""Depreciation"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Understanding Your Books</h3>
                                    <p>You have two main books:</p>
                                    <ul>
                                        <li><strong>GAAP Book</strong> - For financial statements (investors, banks)</li>
                                        <li><strong>Tax Book</strong> - For tax returns (CRA)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>View Book Details</h3>
                                    <p>Each book shows:</p>
                                    <ul>
                                        <li><strong>Default Method</strong> - How depreciation is calculated</li>
                                        <li><strong>Default Convention</strong> - How partial years are handled</li>
                                        <li><strong>GL Accounts</strong> - Where entries are posted</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "run-depreciation", "Run Depreciation" }, { "view-journals", "View Journals" } }
                },

                "schedule-maintenance" => new TaskGuide
                {
                    Title = "How to Schedule Maintenance",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #fef3c7; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Purpose:</strong> Schedule preventive maintenance, repairs, or inspections for your assets.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Maintenance</h3>
                                    <p>Click <a href='/Maintenance' class='task-link'><strong>Maintenance</strong></a> in the left sidebar under ""Operations"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Click Schedule Maintenance</h3>
                                    <p>Click the <strong>+ Schedule Maintenance</strong> button at the top of the page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Select the Asset</h3>
                                    <p>Choose which asset needs maintenance from the dropdown list.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Enter Maintenance Details</h3>
                                    <ul>
                                        <li><strong>Type</strong> - Preventive, Corrective, Inspection, etc.</li>
                                        <li><strong>Description</strong> - What work needs to be done</li>
                                        <li><strong>Scheduled Date</strong> - When the work should happen</li>
                                        <li><strong>Estimated Cost</strong> - Expected cost (optional)</li>
                                        <li><strong>Technician</strong> - Who will do the work (optional)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Save the Work Order</h3>
                                    <p>Click <strong>Schedule</strong>. The work order will appear in your schedule and can be tracked to completion.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "manage-technicians", "Manage Technicians" } }
                },

                "create-cip" => new TaskGuide
                {
                    Title = "How to Create a CIP Project",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #f3e8ff; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Purpose:</strong> Track capital projects from planning through completion, then capitalize as fixed assets.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Capital Projects</h3>
                                    <p>Click <a href='/CIP' class='task-link'><strong>Capital Projects</strong></a> in the left sidebar under ""Operations"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Click New Project</h3>
                                    <p>Click the <strong>+ New Project</strong> button.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Enter Project Details</h3>
                                    <ul>
                                        <li><strong>Project Name</strong> - Descriptive name (e.g., ""New CNC Line Installation"")</li>
                                        <li><strong>Project Number</strong> - Unique identifier</li>
                                        <li><strong>Budget</strong> - Approved budget amount</li>
                                        <li><strong>Start Date</strong> - When work begins</li>
                                        <li><strong>Target Completion</strong> - Expected finish date</li>
                                        <li><strong>Project Manager</strong> - Person responsible</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Add Costs Over Time</h3>
                                    <p>As the project progresses, add costs by clicking <strong>Add Cost</strong> on the project detail page. Costs can be categorized as Labor, Materials, Equipment, Contractor, etc.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Capitalize When Complete</h3>
                                    <p>When the project is finished, click <strong>Capitalize</strong> to convert the accumulated costs into a new fixed asset.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "add-asset", "Add Asset" }, { "schedule-maintenance", "Schedule Maintenance" } }
                },

                "run-inventory" => new TaskGuide
                {
                    Title = "How to Run an Inventory Count",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #e0f2fe; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Purpose:</strong> Verify that your recorded assets exist and are in the correct locations.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Inventory</h3>
                                    <p>Click <a href='/Inventory' class='task-link'><strong>Inventory & Tracking</strong></a> in the left sidebar under ""Assets"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Create New Inventory List</h3>
                                    <p>Click <strong>+ New Inventory List</strong> and give it a name (e.g., ""Q1 2026 Physical Count"").</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Define Scope</h3>
                                    <p>Select which assets to include:</p>
                                    <ul>
                                        <li>All assets</li>
                                        <li>By location (e.g., only MISS plant)</li>
                                        <li>By category or type</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Scan Assets</h3>
                                    <p>As you physically locate each asset, mark it as found by:</p>
                                    <ul>
                                        <li>Scanning its barcode/QR code</li>
                                        <li>Searching and checking off manually</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Review Results</h3>
                                    <p>When complete, review the results showing:</p>
                                    <ul>
                                        <li><strong>Found</strong> - Assets verified</li>
                                        <li><strong>Missing</strong> - Expected but not found</li>
                                        <li><strong>Extra</strong> - Found but not expected</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "export-reports", "Export Reports" } }
                },

                "use-ai-assistant" => new TaskGuide
                {
                    Title = "How to Use the AI Assistant",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Learn how to ask questions about your assets in plain English and get instant, accurate answers with clickable links to relevant data.</p>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Open the AI Assistant</h3>
                                    <p>Click <a href='/AI' class='task-link'><strong>AI Assistant</strong></a> in the left sidebar. You'll see a chat interface where you can type questions.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Type Your Question in Plain English</h3>
                                    <p>Ask anything about your assets, maintenance, or projects. The AI understands natural language, so just ask like you would ask a colleague:</p>
                                    
                                    <div class='info-box'>
                                        <i class='fas fa-comment-dots'></i>
                                        <div class='info-box-content'>
                                            <p><strong>Example Questions:</strong></p>
                                            <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem;'>
                                                <li>""What's our total asset value?""</li>
                                                <li>""Show me overdue maintenance""</li>
                                                <li>""Which CIP projects are over budget?""</li>
                                                <li>""List our top 10 most valuable assets""</li>
                                                <li>""What assets are at the BRAM location?""</li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Review the Response</h3>
                                    <p>The AI analyzes your actual data and provides answers that may include:</p>
                                    <ul>
                                        <li><strong>Summary statistics</strong> - Totals, counts, averages</li>
                                        <li><strong>Lists of items</strong> - Assets, maintenance events, projects</li>
                                        <li><strong>Clickable links</strong> - Jump directly to specific assets or records</li>
                                        <li><strong>Insights</strong> - Trends, warnings, or recommendations</li>
                                    </ul>
                                    
                                    <div class='tip-box'>
                                        <i class='fas fa-lightbulb'></i>
                                        <div class='tip-box-content'>
                                            <p><strong>Tip:</strong> Click any link in the response to go directly to that asset, maintenance event, or project page for more details.</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Ask Follow-Up Questions</h3>
                                    <p>You can continue the conversation with follow-up questions:</p>
                                    <ul>
                                        <li>""Show me more details about that""</li>
                                        <li>""What about just machinery?""</li>
                                        <li>""Filter by location MISS""</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                        
                        <div style='margin-top: 2rem; padding: 1.5rem; background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%); border: 1px solid #86efac; border-radius: 12px;'>
                            <h3 style='margin-top: 0; color: #166534;'><i class='fas fa-magic'></i> What the AI Can Help With</h3>
                            <div style='display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; margin-top: 1rem;'>
                                <div>
                                    <strong style='color: #166534;'>Assets</strong>
                                    <ul style='margin: 0.5rem 0 0 0; color: #166534; font-size: 0.9rem;'>
                                        <li>Total portfolio value</li>
                                        <li>Assets by location</li>
                                        <li>Top assets by value</li>
                                        <li>Depreciation summaries</li>
                                    </ul>
                                </div>
                                <div>
                                    <strong style='color: #166534;'>Maintenance</strong>
                                    <ul style='margin: 0.5rem 0 0 0; color: #166534; font-size: 0.9rem;'>
                                        <li>Overdue maintenance</li>
                                        <li>Upcoming schedules</li>
                                        <li>Technician workload</li>
                                        <li>Maintenance costs</li>
                                    </ul>
                                </div>
                                <div>
                                    <strong style='color: #166534;'>CIP Projects</strong>
                                    <ul style='margin: 0.5rem 0 0 0; color: #166534; font-size: 0.9rem;'>
                                        <li>Budget vs. spending</li>
                                        <li>Over-budget alerts</li>
                                        <li>Project status</li>
                                        <li>Completion progress</li>
                                    </ul>
                                </div>
                                <div>
                                    <strong style='color: #166534;'>Inventory</strong>
                                    <ul style='margin: 0.5rem 0 0 0; color: #166534; font-size: 0.9rem;'>
                                        <li>Scan progress</li>
                                        <li>Missing assets</li>
                                        <li>Location counts</li>
                                        <li>Inventory lists</li>
                                    </ul>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=view-assets'>Browse your assets</a> to explore manually</li>
                                <li><a href='/Help/Tasks?id=export-reports'>Export reports</a> for detailed analysis</li>
                                <li><a href='/Help/Topic?id=ai-assistant'>Learn more about AI Assistant capabilities</a></li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "view-assets", "View Assets" }, { "export-reports", "Export Reports" }, { "schedule-maintenance", "Schedule Maintenance" } }
                },

                "bulk-operations" => new TaskGuide
                {
                    Title = "How to Perform Bulk Operations",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #dbeafe; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Purpose:</strong> Transfer, dispose, or update multiple assets at once instead of one at a time.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Bulk Operations</h3>
                                    <p>Click <a href='/BulkOperations' class='task-link'><strong>Bulk Operations</strong></a> in the left sidebar under ""Assets"".</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Choose Operation Type</h3>
                                    <p>Select what you want to do:</p>
                                    <ul>
                                        <li><strong>Bulk Transfer</strong> - Move multiple assets to a new location</li>
                                        <li><strong>Bulk Status Change</strong> - Activate or deactivate multiple assets</li>
                                        <li><strong>Partial Disposal</strong> - Dispose part of an asset (for component assets)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Select Assets</h3>
                                    <p>Check the boxes next to the assets you want to include in the operation. Use filters to find assets by location, status, or other criteria.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Enter Details</h3>
                                    <p>Depending on the operation, enter:</p>
                                    <ul>
                                        <li><strong>For Transfers:</strong> New location, transfer date, notes</li>
                                        <li><strong>For Status Change:</strong> New status (Active/Inactive)</li>
                                        <li><strong>For Disposal:</strong> Disposal date, proceeds, reason</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Review and Confirm</h3>
                                    <p>Review the summary of changes, then click <strong>Apply</strong> to process all selected assets.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "transfer-asset", "Transfer Asset" }, { "dispose-asset", "Dispose Asset" } }
                },

                "add-attachment" => new TaskGuide
                {
                    Title = "How to Add Attachments",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #f1f5f9; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Purpose:</strong> Attach documents, photos, invoices, or manuals to assets, maintenance events, or CIP projects.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to the Item</h3>
                                    <p>Navigate to the asset, maintenance event, or CIP project you want to add attachments to.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Find the Attachments Section</h3>
                                    <p>Scroll to the <strong>Attachments</strong> section on the detail page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Click Add Attachment</h3>
                                    <p>Click the <strong>+ Add Attachment</strong> button.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Select File and Category</h3>
                                    <ul>
                                        <li><strong>File</strong> - Choose the file to upload</li>
                                        <li><strong>Category</strong> - Invoice, Manual, Photo, Certificate, etc.</li>
                                        <li><strong>Description</strong> - Brief note about the file (optional)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Upload</h3>
                                    <p>Click <strong>Upload</strong>. The file will be stored and linked to the item.</p>
                                </div>
                            </div>
                        </div>
                        
                        <div style='margin-top: 2rem; padding: 1.5rem; background: #fef3c7; border-radius: 8px;'>
                            <h3 style='margin-top: 0; color: #d97706;'>Common Attachment Types</h3>
                            <ul style='margin: 0;'>
                                <li><strong>Purchase invoices</strong> - Proof of cost</li>
                                <li><strong>User manuals</strong> - Equipment documentation</li>
                                <li><strong>Photos</strong> - Asset condition</li>
                                <li><strong>Warranty certificates</strong> - Coverage info</li>
                                <li><strong>Maintenance reports</strong> - Service records</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "add-asset", "Add Asset" }, { "schedule-maintenance", "Schedule Maintenance" } }
                },

                "manage-users" => new TaskGuide
                {
                    Title = "How to Manage Users",
                    Content = @"
                        <div class='task-steps'>
                            <p class='intro-note' style='background: #fee2e2; padding: 1rem; border-radius: 8px; margin-bottom: 1.5rem;'>
                                <strong>Admin Only:</strong> You must have Admin role to manage users.
                            </p>
                            
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Admin</h3>
                                    <p>Click <a href='/Admin/Users' class='task-link'><strong>Admin → Users</strong></a> in the left sidebar.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Add New User</h3>
                                    <p>Click <strong>+ Add User</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Username</strong> - Login name</li>
                                        <li><strong>Email</strong> - For notifications</li>
                                        <li><strong>Role</strong> - Admin, Accountant, or Viewer</li>
                                        <li><strong>Password</strong> - Temporary password</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Edit Existing User</h3>
                                    <p>Click on a user to change their role, reset password, or deactivate their account.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Deactivate User</h3>
                                    <p>To remove access without deleting history, toggle the <strong>Active</strong> status off.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "company-settings", "Company Settings" }, { "manage-technicians", "Manage Technicians" } }
                },

                "company-settings" => new TaskGuide
                {
                    Title = "How to Configure Company Settings",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Company Settings</h3>
                                    <p>Click <a href='/Admin/Company' class='task-link'><strong>Admin → Company</strong></a> in the left sidebar.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Update Company Information</h3>
                                    <ul>
                                        <li><strong>Company Name</strong></li>
                                        <li><strong>Address</strong> - Street, City, Province/State, Postal Code</li>
                                        <li><strong>Tax IDs</strong> - CRA Business Number, GST/HST Number</li>
                                        <li><strong>Currency</strong> - Default currency (CAD, USD, etc.)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Configure Fiscal Year</h3>
                                    <p>Set your fiscal year start month if different from January.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Approval Settings</h3>
                                    <p>Configure whether disposals and transfers require approval:</p>
                                    <ul>
                                        <li>Require approval for disposals</li>
                                        <li>Require approval for transfers</li>
                                        <li>Approval threshold amount</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>5</div>
                                <div class='step-content'>
                                    <h3>Save Changes</h3>
                                    <p>Click <strong>Save</strong> to apply your settings.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-users", "Manage Users" }, { "view-books", "View Books" } }
                },

                "manage-technicians" => new TaskGuide
                {
                    Title = "How to Manage Technicians",
                    Content = @"
                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Technicians</h3>
                                    <p>Click <a href='/Admin/Technicians' class='task-link'><strong>Admin → Technicians</strong></a> in the left sidebar.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Add New Technician</h3>
                                    <p>Click <strong>+ Add Technician</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Name</strong> - Full name</li>
                                        <li><strong>Email</strong> - Contact email</li>
                                        <li><strong>Phone</strong> - Contact number</li>
                                        <li><strong>Specialty</strong> - e.g., Electrical, Mechanical, HVAC</li>
                                        <li><strong>Hourly Rate</strong> - For cost tracking</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Assign to Maintenance</h3>
                                    <p>Once created, technicians can be assigned to maintenance events from the <a href='/Maintenance' class='task-link'>Maintenance</a> page.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Track Workload</h3>
                                    <p>View each technician's assigned work and workload from their profile.</p>
                                </div>
                            </div>
                        </div>
                    ",
                    RelatedTasks = new() { { "schedule-maintenance", "Schedule Maintenance" }, { "manage-users", "Manage Users" } }
                },

                "manage-gl-accounts" => new TaskGuide
                {
                    Title = "How to Manage GL Accounts",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Learn how to set up and manage your Master Files, including creating GL accounts for fixed assets, accumulated depreciation, and expense tracking.</p>
                        </div>

                        <div class='prereq-box'>
                            <h4><i class='fas fa-clipboard-check'></i> Before you begin</h4>
                            <ul>
                                <li>Have your company's GL account structure defined</li>
                                <li>Know which account numbers to use for assets, depreciation, and expenses</li>
                                <li>Understand the difference between debit and credit normal balances</li>
                            </ul>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to GL Accounts</h3>
                                    <p>Navigate to <a href='/Admin/GlAccounts' class='task-link'><strong>Admin → GL Accounts</strong></a> in the Administration section.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Review Pre-Seeded Accounts</h3>
                                    <p>The system comes with 67 pre-configured accounts organized by category:</p>
                                    <ul>
                                        <li><strong>1000s</strong> - Cash, Receivables, Inventory</li>
                                        <li><strong>1500-1900s</strong> - Fixed Assets (Buildings, Machinery, Vehicles, Technology, Tooling)</li>
                                        <li><strong>1950s</strong> - Accumulated Depreciation accounts</li>
                                        <li><strong>2000s</strong> - Liabilities</li>
                                        <li><strong>6000s</strong> - Depreciation and Maintenance Expenses</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Add a New Account</h3>
                                    <p>Click <strong>+ Add Account</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Account Number</strong> - Your GL account code (e.g., ""1610"")</li>
                                        <li><strong>Account Name</strong> - Description (e.g., ""Machinery - CNC"")</li>
                                        <li><strong>Account Type</strong> - Asset, Liability, Expense, ContraAsset, etc.</li>
                                        <li><strong>Category</strong> - Functional grouping for reporting</li>
                                        <li><strong>Normal Balance</strong> - Debit or Credit</li>
                                    </ul>
                                    
                                    <div class='tip-box'>
                                        <i class='fas fa-lightbulb'></i>
                                        <div class='tip-box-content'>
                                            <p><strong>Tip:</strong> Asset and Expense accounts normally have debit balances. Liability, Equity, and Revenue accounts normally have credit balances. ContraAsset accounts (like Accumulated Depreciation) have credit balances.</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Filter and Search</h3>
                                    <p>Use the dropdown filters to view accounts by Category or Type, making it easy to find specific accounts.</p>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-asset-categories'>Set up Asset Categories</a> with GL account mappings</li>
                                <li><a href='/Help/Tasks?id=manage-cost-centers'>Define Cost Centers</a> for multi-plant tracking</li>
                                <li><a href='/Help/Tasks?id=manage-departments'>Create Departments</a> for cost allocation</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-asset-categories", "Manage Asset Categories" }, { "manage-cost-centers", "Manage Cost Centers" }, { "manage-departments", "Manage Departments" } }
                },

                "manage-cost-centers" => new TaskGuide
                {
                    Title = "How to Manage Cost Centers",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Set up cost centers to track assets across multiple plants, buildings, production lines, and work cells for accurate location-based reporting.</p>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Cost Centers</h3>
                                    <p>Navigate to <a href='/Admin/CostCenters' class='task-link'><strong>Admin → Cost Centers</strong></a> in the Administration section.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Understand the Hierarchy</h3>
                                    <p>Cost centers support a multi-level hierarchy:</p>
                                    <ul>
                                        <li><strong>Corporate</strong> - Headquarters level</li>
                                        <li><strong>Region</strong> - Geographic regions</li>
                                        <li><strong>Plant</strong> - Manufacturing facilities</li>
                                        <li><strong>Building</strong> - Structures within a plant</li>
                                        <li><strong>Production Line</strong> - Assembly or production lines</li>
                                        <li><strong>Work Cell</strong> - Individual work stations</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Add a Cost Center</h3>
                                    <p>Click <strong>+ Add Cost Center</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Code</strong> - Short identifier (e.g., ""PLANT1"")</li>
                                        <li><strong>Name</strong> - Full name (e.g., ""Main Manufacturing Plant"")</li>
                                        <li><strong>Type</strong> - Level in the hierarchy</li>
                                        <li><strong>Parent</strong> - Higher-level cost center (optional)</li>
                                        <li><strong>Location</strong> - City, State, Country</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Link to Assets</h3>
                                    <p>Once created, cost centers can be assigned to assets for detailed location tracking and cost allocation reporting.</p>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-departments'>Create Departments</a> linked to cost centers</li>
                                <li><a href='/Help/Tasks?id=manage-gl-accounts'>Configure GL Accounts</a> for financial tracking</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-departments", "Manage Departments" }, { "manage-gl-accounts", "Manage GL Accounts" } }
                },

                "manage-departments" => new TaskGuide
                {
                    Title = "How to Manage Departments",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Create departments to organize assets by business function and allocate costs appropriately across your organization.</p>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Departments</h3>
                                    <p>Navigate to <a href='/Admin/Departments' class='task-link'><strong>Admin → Departments</strong></a> in the Administration section.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Department Types</h3>
                                    <p>The system supports 14 standard department types:</p>
                                    <ul>
                                        <li><strong>Executive, Finance, HR</strong> - Corporate functions</li>
                                        <li><strong>Operations, Production</strong> - Manufacturing</li>
                                        <li><strong>Maintenance, Facilities</strong> - Asset upkeep</li>
                                        <li><strong>Quality, Engineering</strong> - Technical functions</li>
                                        <li><strong>Warehouse, Shipping</strong> - Logistics</li>
                                        <li><strong>IT, Safety, Purchasing</strong> - Support functions</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Add a Department</h3>
                                    <p>Click <strong>+ Add Department</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Code</strong> - Short identifier (e.g., ""MAINT"")</li>
                                        <li><strong>Name</strong> - Full name (e.g., ""Maintenance Department"")</li>
                                        <li><strong>Type</strong> - Select from standard types</li>
                                        <li><strong>Cost Center</strong> - Link to a location (optional)</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Assign to Assets</h3>
                                    <p>Departments can be assigned to assets for departmental cost allocation and responsibility tracking.</p>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-asset-categories'>Set up Asset Categories</a></li>
                                <li><a href='/Help/Tasks?id=add-asset'>Add assets</a> and assign them to departments</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-cost-centers", "Manage Cost Centers" }, { "manage-asset-categories", "Manage Asset Categories" } }
                },

                "manage-asset-categories" => new TaskGuide
                {
                    Title = "How to Manage Asset Categories",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Configure asset categories with default depreciation settings and GL account mappings to streamline asset entry and ensure consistent accounting treatment.</p>
                        </div>

                        <div class='task-steps'>
                            <div class='step'>
                                <div class='step-number'>1</div>
                                <div class='step-content'>
                                    <h3>Go to Asset Categories</h3>
                                    <p>Navigate to <a href='/Admin/AssetCategories' class='task-link'><strong>Admin → Asset Categories</strong></a> in the Administration section.</p>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>2</div>
                                <div class='step-content'>
                                    <h3>Review Pre-Configured Categories</h3>
                                    <p>The system includes 9 common categories:</p>
                                    <ul>
                                        <li><strong>Buildings</strong> - 39-year MACRS, 39-year useful life</li>
                                        <li><strong>Machinery & Equipment</strong> - 7-year MACRS</li>
                                        <li><strong>CNC Equipment, Welding</strong> - Specialized machinery</li>
                                        <li><strong>Forklifts, Vehicles</strong> - 5-year MACRS</li>
                                        <li><strong>Computers & IT</strong> - 5-year MACRS</li>
                                        <li><strong>Tooling</strong> - Dies, molds, fixtures</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>3</div>
                                <div class='step-content'>
                                    <h3>Add a Category</h3>
                                    <p>Click <strong>+ Add Category</strong> and configure:</p>
                                    <ul>
                                        <li><strong>Code & Name</strong> - Identifier and description</li>
                                        <li><strong>MACRS Class</strong> - Default US tax class (3, 5, 7, 15, 27.5, 39 year)</li>
                                        <li><strong>Useful Life</strong> - Default book depreciation period</li>
                                        <li><strong>Salvage %</strong> - Expected residual value percentage</li>
                                    </ul>
                                    
                                    <div class='info-box'>
                                        <i class='fas fa-info-circle'></i>
                                        <div class='info-box-content'>
                                            <p><strong>Pro Tip:</strong> Setting defaults here saves time when adding assets - the system will auto-populate depreciation settings based on the selected category.</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='step'>
                                <div class='step-number'>4</div>
                                <div class='step-content'>
                                    <h3>Map GL Accounts</h3>
                                    <p>Link each category to the appropriate GL accounts:</p>
                                    <ul>
                                        <li><strong>Asset Account</strong> - Where the asset cost is recorded (e.g., 1600 - Machinery)</li>
                                        <li><strong>Accumulated Depreciation</strong> - Contra-asset account (e.g., 1960 - Accum Dep Machinery)</li>
                                        <li><strong>Depreciation Expense</strong> - P&L expense account (e.g., 6010 - Depreciation - Machinery)</li>
                                    </ul>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=add-asset'>Add new assets</a> using your configured categories</li>
                                <li><a href='/Help/Tasks?id=run-depreciation'>Run depreciation</a> to calculate monthly expenses</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-gl-accounts", "Manage GL Accounts" }, { "add-asset", "Add Asset" }, { "run-depreciation", "Run Depreciation" } }
                },

                "manage-vendors" => new TaskGuide
                {
                    Title = "How to Manage Vendors",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Set up and manage your vendor/supplier list for asset procurement. Vendors can be assigned to assets to track where equipment was purchased and maintain supplier relationships.</p>
                        </div>

                        <div class='callout callout-info'>
                            <div class='callout-icon'>
                                <svg fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'/></svg>
                            </div>
                            <div class='callout-content'>
                                <h5>Vendors vs. Manufacturers</h5>
                                <p><strong>Vendors</strong> are who you buy from (suppliers, distributors). <strong>Manufacturers</strong> are who makes the equipment (OEMs). An asset can have both - you might buy a Haas CNC (manufacturer) from Industrial Machinery Corp (vendor).</p>
                            </div>
                        </div>

                        <div class='visual-steps'>
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>1</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-compass'></i></span> Navigate to Vendors</h4>
                                    <p>Go to <a href='/Admin/Vendors' class='task-link'><strong>Admin → Vendors</strong></a> in the Administration section. You'll find it under ""Personnel & Resources"" in the Admin Hub.</p>
                                    <div class='nav-path'>
                                        <span class='nav-path-item'><i class='fas fa-home nav-path-icon'></i> Dashboard</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item'><i class='fas fa-cog nav-path-icon'></i> Admin</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item current'><i class='fas fa-truck nav-path-icon'></i> Vendors</span>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>2</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-list'></i></span> Review Existing Vendors</h4>
                                    <p>The system includes 10 pre-configured vendors commonly used in manufacturing:</p>
                                    <ul>
                                        <li><strong>Haas Automation</strong> - CNC machinery</li>
                                        <li><strong>Caterpillar Inc.</strong> - Heavy equipment, forklifts</li>
                                        <li><strong>Stanley Black & Decker</strong> - Power tools</li>
                                        <li><strong>Grainger</strong> - Industrial supplies</li>
                                        <li><strong>MSC Industrial</strong> - Cutting tools, MRO</li>
                                        <li><strong>Dell Technologies</strong> - Computers, servers</li>
                                        <li><strong>Lincoln Electric</strong> - Welding equipment</li>
                                        <li><strong>Toyota Material Handling</strong> - Forklifts</li>
                                        <li><strong>McMaster-Carr</strong> - Hardware, components</li>
                                        <li><strong>Fastenal</strong> - Fasteners, safety</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>3</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-plus-circle'></i></span> Add a New Vendor</h4>
                                    <p>Click <strong>+ Add Vendor</strong> and fill in the vendor details:</p>
                                    <div class='field-table'>
                                        <div class='field-row'>
                                            <div class='field-name'>Code <span class='required'>*</span></div>
                                            <div class='field-description'>Short unique identifier (e.g., ""HAAS"", ""CAT"", ""GRAINGER"")</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Name <span class='required'>*</span></div>
                                            <div class='field-description'>Full company name (e.g., ""Haas Automation, Inc."")</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Contact Person</div>
                                            <div class='field-description'>Your primary sales rep or account manager</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Phone / Email</div>
                                            <div class='field-description'>Contact information for orders and support</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Website</div>
                                            <div class='field-description'>Vendor's website URL for quick reference</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Address</div>
                                            <div class='field-description'>Street, City, Region, Postal Code, Country</div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>4</div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-link'></i></span> Assign Vendors to Assets</h4>
                                    <p>When creating or editing an asset, select the vendor from the dropdown. This helps you:</p>
                                    <ul>
                                        <li>Track procurement sources for audit purposes</li>
                                        <li>Quickly find vendor contacts for warranty claims</li>
                                        <li>Analyze spending by vendor in reports</li>
                                    </ul>
                                    
                                    <div class='callout callout-tip'>
                                        <div class='callout-icon'>
                                            <svg fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z'/></svg>
                                        </div>
                                        <div class='callout-content'>
                                            <h5>Pro Tip</h5>
                                            <p>Use the AI Assistant to ask questions like ""Which vendor did we buy the most equipment from?"" or ""Show me all assets from Haas Automation.""</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-manufacturers'>Set up Manufacturers</a> (OEMs who make the equipment)</li>
                                <li><a href='/Help/Tasks?id=add-asset'>Add assets</a> and assign vendors</li>
                                <li><a href='/Help/Tasks?id=manage-locations'>Configure Locations</a> where assets are installed</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-manufacturers", "Manage Manufacturers" }, { "add-asset", "Add Asset" }, { "manage-locations", "Manage Locations" } }
                },

                "manage-manufacturers" => new TaskGuide
                {
                    Title = "How to Manage Manufacturers",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Maintain your equipment manufacturer (OEM) list for accurate asset tracking. Manufacturers help identify the original equipment maker for warranty, parts, and service purposes.</p>
                        </div>

                        <div class='visual-steps'>
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>1</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-compass'></i></span> Navigate to Manufacturers</h4>
                                    <p>Go to <a href='/Admin/Manufacturers' class='task-link'><strong>Admin → Manufacturers</strong></a> in the Administration section under ""Personnel & Resources.""</p>
                                    <div class='nav-path'>
                                        <span class='nav-path-item'><i class='fas fa-home nav-path-icon'></i> Dashboard</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item'><i class='fas fa-cog nav-path-icon'></i> Admin</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item current'><i class='fas fa-industry nav-path-icon'></i> Manufacturers</span>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>2</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-list'></i></span> Review Existing Manufacturers</h4>
                                    <p>The system includes pre-seeded manufacturers for common industrial equipment:</p>
                                    <ul>
                                        <li><strong>CNC/Machinery:</strong> Haas, Mazak, DMG Mori, Okuma, Fanuc</li>
                                        <li><strong>Welding:</strong> Lincoln Electric, Miller, ESAB</li>
                                        <li><strong>Material Handling:</strong> Toyota, Crown, Hyster, Yale</li>
                                        <li><strong>Metrology:</strong> Mitutoyo, Zeiss, Hexagon</li>
                                        <li><strong>Automation:</strong> ABB, KUKA, Universal Robots</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>3</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-plus-circle'></i></span> Add a Manufacturer</h4>
                                    <p>Click <strong>+ Add Manufacturer</strong> and enter:</p>
                                    <ul>
                                        <li><strong>Name</strong> - Company name (e.g., ""Haas Automation"")</li>
                                        <li><strong>Country</strong> - Where they're headquartered</li>
                                        <li><strong>Website</strong> - For parts lookup and manuals</li>
                                        <li><strong>Notes</strong> - Support contacts, warranty terms</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>4</div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-cogs'></i></span> Use in Asset Management</h4>
                                    <p>Manufacturers are assigned to assets to help with:</p>
                                    <ul>
                                        <li>Identifying OEM for warranty claims</li>
                                        <li>Finding replacement parts and manuals</li>
                                        <li>Scheduling OEM-recommended maintenance</li>
                                        <li>Analyzing equipment reliability by brand</li>
                                    </ul>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-vendors'>Set up Vendors</a> (where you purchase equipment)</li>
                                <li><a href='/Help/Tasks?id=add-asset'>Add assets</a> and assign manufacturers</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-vendors", "Manage Vendors" }, { "add-asset", "Add Asset" } }
                },

                "manage-locations" => new TaskGuide
                {
                    Title = "How to Manage Locations",
                    Content = @"
                        <div class='task-intro'>
                            <h4><i class='fas fa-bullseye'></i> What you'll accomplish</h4>
                            <p>Configure your location hierarchy for accurate asset tracking. Locations can be organized in a tree structure from plants down to specific bays or work cells.</p>
                        </div>

                        <div class='callout callout-info'>
                            <div class='callout-icon'>
                                <svg fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z'/><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M15 11a3 3 0 11-6 0 3 3 0 016 0z'/></svg>
                            </div>
                            <div class='callout-content'>
                                <h5>Location Hierarchy</h5>
                                <p>Locations support a hierarchical structure: <strong>Plant → Building → Area → Bay/Station</strong>. This allows for detailed asset tracking while maintaining the ability to report at any level.</p>
                            </div>
                        </div>

                        <div class='visual-steps'>
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>1</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-compass'></i></span> Navigate to Locations</h4>
                                    <p>Go to <a href='/Admin/Locations' class='task-link'><strong>Admin → Locations</strong></a> in the Administration section.</p>
                                    <div class='nav-path'>
                                        <span class='nav-path-item'><i class='fas fa-home nav-path-icon'></i> Dashboard</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item'><i class='fas fa-cog nav-path-icon'></i> Admin</span>
                                        <span class='nav-path-arrow'>→</span>
                                        <span class='nav-path-item current'><i class='fas fa-map-marker-alt nav-path-icon'></i> Locations</span>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>2</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-sitemap'></i></span> Location Types</h4>
                                    <p>The system supports these location types:</p>
                                    <ul>
                                        <li><strong>Plant</strong> - Top-level facility (e.g., ""Main Plant"")</li>
                                        <li><strong>Building</strong> - Structure within a plant (e.g., ""Building A"")</li>
                                        <li><strong>Area</strong> - Functional area (e.g., ""Machine Shop"")</li>
                                        <li><strong>Bay</strong> - Specific section (e.g., ""Bay 1"")</li>
                                        <li><strong>Station</strong> - Work cell or position (e.g., ""CNC Cell 1"")</li>
                                        <li><strong>Warehouse</strong> - Storage facility</li>
                                        <li><strong>Yard</strong> - Outdoor storage area</li>
                                    </ul>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>3</div>
                                    <div class='visual-step-line'></div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-plus-circle'></i></span> Add a Location</h4>
                                    <p>Click <strong>+ Add Location</strong> and enter:</p>
                                    <div class='field-table'>
                                        <div class='field-row'>
                                            <div class='field-name'>Code <span class='required'>*</span></div>
                                            <div class='field-description'>Short identifier (e.g., ""BLDG-A"", ""BAY-01"")</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Name <span class='required'>*</span></div>
                                            <div class='field-description'>Full name (e.g., ""Building A - Machine Shop"")</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Type</div>
                                            <div class='field-description'>Select from Plant, Building, Area, Bay, Station, etc.</div>
                                        </div>
                                        <div class='field-row'>
                                            <div class='field-name'>Parent Location</div>
                                            <div class='field-description'>Select parent for hierarchy (e.g., Bay belongs to Building)</div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <div class='visual-step'>
                                <div class='visual-step-indicator'>
                                    <div class='visual-step-number'>4</div>
                                </div>
                                <div class='visual-step-content'>
                                    <h4><span class='step-icon'><i class='fas fa-exchange-alt'></i></span> Asset Transfers</h4>
                                    <p>When assets move between locations, use the <a href='/Help/Tasks?id=transfer-asset' class='task-link'>Transfer Asset</a> feature to:</p>
                                    <ul>
                                        <li>Update the asset's current location</li>
                                        <li>Maintain a complete transfer history</li>
                                        <li>Record the reason for the move</li>
                                    </ul>
                                </div>
                            </div>
                        </div>

                        <div class='whats-next'>
                            <h4><i class='fas fa-arrow-circle-right'></i> What's Next?</h4>
                            <ul>
                                <li><a href='/Help/Tasks?id=manage-cost-centers'>Set up Cost Centers</a> for financial tracking</li>
                                <li><a href='/Help/Tasks?id=transfer-asset'>Learn to transfer assets</a> between locations</li>
                                <li><a href='/Help/Tasks?id=run-inventory'>Run a physical inventory</a> to verify locations</li>
                            </ul>
                        </div>
                    ",
                    RelatedTasks = new() { { "manage-cost-centers", "Manage Cost Centers" }, { "transfer-asset", "Transfer Asset" }, { "run-inventory", "Run Inventory" } }
                },

                _ => null
            };
        }

        private class TaskGuide
        {
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
            public Dictionary<string, string> RelatedTasks { get; set; } = new();
        }
    }
}
