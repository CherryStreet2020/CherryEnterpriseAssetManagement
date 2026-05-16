using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Help
{
    public class TopicModel : PageModel
    {
        public string Title { get; set; } = "";
        public new string Content { get; set; } = "";
        public Dictionary<string, string> RelatedTopics { get; set; } = new();

        public IActionResult OnGet(string id)
        {
            var topic = GetTopic(id ?? "");
            if (topic == null)
                return RedirectToPage("/Help/Index");

            Title = topic.Title;
            Content = topic.Content;
            RelatedTopics = topic.RelatedTopics;
            return Page();
        }

        private static HelpTopic? GetTopic(string id)
        {
            return id.ToLower() switch
            {
                "dashboard" => new HelpTopic
                {
                    Title = "Understanding the Dashboard",
                    Content = @"
                        <h2>What You See on the Dashboard</h2>
                        <p>The Dashboard is your ""home base"" - it shows you the big picture of all your company's fixed assets at a glance.</p>
                        
                        <h3>The Numbers Explained</h3>
                        
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #3b82f6; margin-top: 0;'>Active Assets</h4>
                            <p>This is how many assets your company currently owns and uses. ""Active"" means they're still in service - not sold, scrapped, or retired.</p>
                        </div>
                        
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #22c55e; margin-top: 0;'>Total Acquisition Cost</h4>
                            <p>This is what you <strong>originally paid</strong> for all your assets combined. Think of it as the total ""sticker price"" of everything you bought.</p>
                            <p><em>Example: If you bought a machine for $100,000, that's part of this total.</em></p>
                        </div>
                        
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #f59e0b; margin-top: 0;'>Accumulated Depreciation</h4>
                            <p>This is how much value your assets have ""used up"" over time. Assets lose value as they age and get used - this number tracks that loss.</p>
                            <p><em>Example: A $100,000 machine that's 5 years into a 10-year life has depreciated about $50,000.</em></p>
                        </div>
                        
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #8b5cf6; margin-top: 0;'>Fair Market Value (FMV)</h4>
                            <p>This is what your assets are <strong>worth today</strong> if you tried to sell them. It's an estimate based on current market conditions.</p>
                            <p><em>Note: FMV can be different from book value because markets change.</em></p>
                        </div>
                        
                        <h3>The Simple Formula</h3>
                        <p style='font-size: 1.2rem; background: #dbeafe; padding: 1rem; border-radius: 8px; text-align: center;'>
                            <strong>Book Value = Acquisition Cost - Accumulated Depreciation</strong>
                        </p>
                        <p>This tells you what your assets are ""worth"" on your accounting books.</p>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "depreciation", "Depreciation" } }
                },

                "assets" => new HelpTopic
                {
                    Title = "Understanding Assets",
                    Content = @"
                        <h2>What is an Asset?</h2>
                        <p>An <strong>asset</strong> is something valuable your company owns that:</p>
                        <ul>
                            <li>Lasts more than one year</li>
                            <li>Helps your business operate</li>
                            <li>Has significant value (usually over $1,000)</li>
                        </ul>
                        
                        <h3>Examples of Fixed Assets</h3>
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Asset Type</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Examples</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Machinery</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>CNC machines, lathes, mills, presses</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Vehicles</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Trucks, forklifts, company cars</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Buildings</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Factories, warehouses, offices</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Equipment</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Computers, tools, furniture</td>
                            </tr>
                        </table>
                        
                        <h3>Asset Lifecycle</h3>
                        <div style='display: flex; gap: 1rem; flex-wrap: wrap; margin: 1rem 0;'>
                            <div style='flex: 1; min-width: 150px; background: #dcfce7; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>1. Purchase</strong><br>You buy it
                            </div>
                            <div style='flex: 1; min-width: 150px; background: #dbeafe; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>2. Use</strong><br>It helps your business
                            </div>
                            <div style='flex: 1; min-width: 150px; background: #fef3c7; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>3. Depreciate</strong><br>It loses value over time
                            </div>
                            <div style='flex: 1; min-width: 150px; background: #fee2e2; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>4. Dispose</strong><br>Sell, scrap, or retire
                            </div>
                        </div>
                        
                        <h3>Key Information for Each Asset</h3>
                        <ul>
                            <li><strong>Asset Number</strong> - Unique ID to identify it</li>
                            <li><strong>Description</strong> - What it is</li>
                            <li><strong>Location</strong> - Where it's located</li>
                            <li><strong>Acquisition Cost</strong> - What you paid</li>
                            <li><strong>In-Service Date</strong> - When you started using it</li>
                            <li><strong>Useful Life</strong> - How long you expect it to last</li>
                        </ul>
                    ",
                    RelatedTopics = new() { { "depreciation", "Depreciation" }, { "books", "Books" } }
                },

                "depreciation" => new HelpTopic
                {
                    Title = "Understanding Depreciation",
                    Content = @"
                        <h2>What is Depreciation?</h2>
                        <p>Depreciation is an accounting way of saying: <strong>""This asset is losing value over time.""</strong></p>
                        
                        <p>Think of it like a car. A brand new car loses value the moment you drive it off the lot. After 5 years, it's worth less than when you bought it. That loss in value is depreciation.</p>
                        
                        <h3>Why Do We Track Depreciation?</h3>
                        <ol>
                            <li><strong>Accurate financials</strong> - Shows the true value of what you own</li>
                            <li><strong>Tax deductions</strong> - You can deduct depreciation from your taxes</li>
                            <li><strong>Planning</strong> - Know when assets need replacement</li>
                        </ol>
                        
                        <h3>A Simple Example</h3>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <p><strong>You buy a machine for $100,000</strong></p>
                            <p>You expect it to last 10 years (useful life)</p>
                            <p>At the end, it'll be worth $10,000 (salvage value)</p>
                            <hr style='margin: 1rem 0;'>
                            <p><strong>Annual Depreciation = ($100,000 - $10,000) ÷ 10 years = $9,000/year</strong></p>
                            <p>Each year, you record $9,000 as depreciation expense.</p>
                        </div>
                        
                        <h3>Depreciation Methods</h3>
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Method</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>How It Works</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Best For</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Straight-Line</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Same amount every year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Most assets, simple and predictable</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Declining Balance</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>More at first, less later</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Tech equipment that loses value fast</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Units of Production</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Based on usage</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Machines with measurable output</td>
                            </tr>
                        </table>
                    ",
                    RelatedTopics = new() { { "books", "Books" }, { "journals", "Journals" }, { "cca", "CCA (Tax)" } }
                },

                "books" => new HelpTopic
                {
                    Title = "Understanding Books",
                    Content = @"
                        <h2>What are Depreciation Books?</h2>
                        <p>A ""book"" is just a way to track an asset's value. You might track the same asset in <strong>different ways for different purposes</strong>.</p>
                        
                        <h3>Why Multiple Books?</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #1d4ed8; margin-top: 0;'>GAAP Book</h4>
                                <p><strong>Purpose:</strong> Financial reporting</p>
                                <p>Uses rules from ""Generally Accepted Accounting Principles"" to show the true economic value of your assets.</p>
                                <p><em>Used for: Investors, banks, financial statements</em></p>
                            </div>
                            <div style='background: #dcfce7; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #16a34a; margin-top: 0;'>Tax Book</h4>
                                <p><strong>Purpose:</strong> Tax deductions</p>
                                <p>Uses rules from the government (CRA in Canada) to calculate how much depreciation you can deduct from taxes.</p>
                                <p><em>Used for: Tax returns, CRA filings</em></p>
                            </div>
                        </div>
                        
                        <h3>Same Asset, Different Values</h3>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <p><strong>Example: A $100,000 machine after 3 years</strong></p>
                            <table style='width: 100%; margin-top: 1rem;'>
                                <tr>
                                    <td><strong>GAAP Book Value:</strong></td>
                                    <td>$70,000 (using 10-year straight-line)</td>
                                </tr>
                                <tr>
                                    <td><strong>Tax Book Value:</strong></td>
                                    <td>$51,200 (using CCA declining balance)</td>
                                </tr>
                            </table>
                            <p style='margin-top: 1rem;'><em>Both are correct - they just serve different purposes!</em></p>
                        </div>
                    ",
                    RelatedTopics = new() { { "depreciation", "Depreciation" }, { "cca", "CCA (Tax)" } }
                },

                "journals" => new HelpTopic
                {
                    Title = "Understanding Journal Entries",
                    Content = @"
                        <h2>What is a Journal Entry?</h2>
                        <p>A journal entry is how accountants record financial transactions. Think of it as a ""receipt"" that says money moved from one place to another.</p>
                        
                        <h3>The Golden Rule: Debits = Credits</h3>
                        <p>Every journal entry has two sides that must be equal:</p>
                        <ul>
                            <li><strong>Debit (left side)</strong> - Where the money ""went to""</li>
                            <li><strong>Credit (right side)</strong> - Where the money ""came from""</li>
                        </ul>
                        
                        <h3>Example: Recording Depreciation</h3>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <p><strong>Monthly depreciation of $5,000</strong></p>
                            <table style='width: 100%; border-collapse: collapse; margin-top: 1rem;'>
                                <tr style='background: #e2e8f0;'>
                                    <th style='padding: 0.5rem; text-align: left;'>Account</th>
                                    <th style='padding: 0.5rem; text-align: right;'>Debit</th>
                                    <th style='padding: 0.5rem; text-align: right;'>Credit</th>
                                </tr>
                                <tr>
                                    <td style='padding: 0.5rem;'>Depreciation Expense</td>
                                    <td style='padding: 0.5rem; text-align: right;'>$5,000</td>
                                    <td style='padding: 0.5rem; text-align: right;'>-</td>
                                </tr>
                                <tr>
                                    <td style='padding: 0.5rem;'>Accumulated Depreciation</td>
                                    <td style='padding: 0.5rem; text-align: right;'>-</td>
                                    <td style='padding: 0.5rem; text-align: right;'>$5,000</td>
                                </tr>
                                <tr style='font-weight: bold; background: #f1f5f9;'>
                                    <td style='padding: 0.5rem;'>Total</td>
                                    <td style='padding: 0.5rem; text-align: right;'>$5,000</td>
                                    <td style='padding: 0.5rem; text-align: right;'>$5,000</td>
                                </tr>
                            </table>
                            <p style='margin-top: 1rem;'><em>See how they balance? That's always required!</em></p>
                        </div>
                        
                        <h3>Generating Depreciation Journals</h3>
                        <p>This software can automatically create depreciation journal entries each month. Just:</p>
                        <ol>
                            <li>Go to the Journals page</li>
                            <li>Click ""Generate Depreciation""</li>
                            <li>Select the month and book</li>
                            <li>Review and post the entries</li>
                        </ol>
                    ",
                    RelatedTopics = new() { { "depreciation", "Depreciation" }, { "books", "Books" } }
                },

                "cca" => new HelpTopic
                {
                    Title = "Understanding CCA (Canadian Tax)",
                    Content = @"
                        <h2>What is CCA?</h2>
                        <p><strong>Capital Cost Allowance (CCA)</strong> is Canada's tax depreciation system. It's how the government lets you deduct the cost of assets from your taxable income.</p>
                        
                        <h3>CCA Classes</h3>
                        <p>The CRA groups assets into ""classes"" based on what they are. Each class has a specific depreciation rate.</p>
                        
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Class</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Rate</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>What's Included</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Class 1</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>4%</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Buildings</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Class 8</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>20%</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Furniture, equipment, machinery (general)</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Class 10</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>30%</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Vehicles, automotive equipment</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Class 43</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>30%</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Manufacturing equipment</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Class 50</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>55%</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Computers and software</td>
                            </tr>
                        </table>
                        
                        <h3>The Half-Year Rule</h3>
                        <div style='background: #fef3c7; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <p><strong>Important!</strong> In the year you buy an asset, you can only claim <strong>half</strong> the normal CCA.</p>
                            <p><em>Example: You buy a $10,000 computer (Class 50, 55% rate) in 2024.</em></p>
                            <p>Normal CCA would be: $10,000 × 55% = $5,500</p>
                            <p>But with half-year rule: $5,500 ÷ 2 = <strong>$2,750</strong> for the first year</p>
                        </div>
                        
                        <h3>UCC (Undepreciated Capital Cost)</h3>
                        <p>UCC is the ""book value"" for tax purposes. It's what's left after you've claimed CCA over the years.</p>
                        <p style='background: #dbeafe; padding: 1rem; border-radius: 8px; text-align: center;'>
                            <strong>UCC = Original Cost - Total CCA Claimed</strong>
                        </p>
                    ",
                    RelatedTopics = new() { { "depreciation", "Depreciation" }, { "books", "Books" } }
                },

                "reports" => new HelpTopic
                {
                    Title = "Understanding Reports & Exports",
                    Content = @"
                        <h2>Getting Your Data Out</h2>
                        <p>The Reports section lets you export your asset data in different formats for your accountant, auditors, or your own records.</p>
                        
                        <h3>Export Formats</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dcfce7; padding: 1.5rem; border-radius: 8px; text-align: center;'>
                                <i class='fas fa-file-csv' style='font-size: 2rem; color: #16a34a;'></i>
                                <h4>CSV</h4>
                                <p>Opens in Excel, Google Sheets, or any spreadsheet app</p>
                            </div>
                            <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px; text-align: center;'>
                                <i class='fas fa-file-excel' style='font-size: 2rem; color: #1d4ed8;'></i>
                                <h4>Excel</h4>
                                <p>Formatted spreadsheet with headers and styling</p>
                            </div>
                            <div style='background: #fee2e2; padding: 1.5rem; border-radius: 8px; text-align: center;'>
                                <i class='fas fa-file-pdf' style='font-size: 2rem; color: #dc2626;'></i>
                                <h4>PDF</h4>
                                <p>Professional report ready to print or email</p>
                            </div>
                        </div>
                        
                        <h3>Available Reports</h3>
                        <ul>
                            <li><strong>Asset Register</strong> - Complete list of all your assets with costs and depreciation</li>
                            <li><strong>Journal Entries</strong> - All depreciation and transaction records</li>
                            <li><strong>CCA Report</strong> - Tax depreciation by class for CRA filing</li>
                        </ul>
                        
                        <h3>How to Export</h3>
                        <ol>
                            <li>Go to the <strong>Reports</strong> page</li>
                            <li>Find the report you need</li>
                            <li>Click the format you want (CSV, Excel, or PDF)</li>
                            <li>The file will download automatically</li>
                        </ol>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "cca", "CCA (Tax)" } }
                },

                "glossary" => new HelpTopic
                {
                    Title = "Glossary of Terms",
                    Content = @"
                        <h2>Accounting & Asset Management Terms</h2>
                        
                        <div style='display: flex; flex-direction: column; gap: 1rem;'>
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Acquisition Cost</strong>
                                <p style='margin: 0.5rem 0 0 0;'>What you paid for an asset, including delivery and setup costs.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Accumulated Depreciation</strong>
                                <p style='margin: 0.5rem 0 0 0;'>The total depreciation recorded since you bought the asset. It increases every year.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Book Value (Net Book Value)</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Acquisition Cost minus Accumulated Depreciation. What the asset is ""worth"" on your books.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Bonus Depreciation</strong>
                                <p style='margin: 0.5rem 0 0 0;'>US tax incentive allowing immediate deduction of a percentage of asset cost in the first year.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>CCA (Capital Cost Allowance)</strong>
                                <p style='margin: 0.5rem 0 0 0;'>The Canadian tax system for deducting asset costs over time.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>CIP (Construction-in-Progress)</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Capital projects being built that will become fixed assets when completed.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Depreciation</strong>
                                <p style='margin: 0.5rem 0 0 0;'>The decrease in an asset's value over time due to wear, age, or obsolescence.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Disposal</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Getting rid of an asset - by selling it, scrapping it, or writing it off.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Fair Market Value (FMV)</strong>
                                <p style='margin: 0.5rem 0 0 0;'>What someone would actually pay for the asset today on the open market.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>GAAP</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Generally Accepted Accounting Principles - the standard rules for financial reporting.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>In-Service Date</strong>
                                <p style='margin: 0.5rem 0 0 0;'>When you started using the asset. Depreciation begins from this date.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>MACRS</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Modified Accelerated Cost Recovery System - the US tax depreciation method for most assets.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Preventive Maintenance</strong>
                                <p style='margin: 0.5rem 0 0 0;'>Scheduled maintenance performed to prevent equipment failure before it occurs.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Salvage Value</strong>
                                <p style='margin: 0.5rem 0 0 0;'>What you expect the asset to be worth at the end of its useful life.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Section 179</strong>
                                <p style='margin: 0.5rem 0 0 0;'>US tax provision allowing immediate expensing of qualifying equipment purchases.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>UCC (Undepreciated Capital Cost)</strong>
                                <p style='margin: 0.5rem 0 0 0;'>The remaining value of assets for tax purposes after CCA has been claimed.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Useful Life</strong>
                                <p style='margin: 0.5rem 0 0 0;'>How long you expect to use the asset before replacing it.</p>
                            </div>
                            
                            <div style='background: #f8fafc; padding: 1rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <strong>Work Order</strong>
                                <p style='margin: 0.5rem 0 0 0;'>A formal request to perform maintenance, repair, or service on an asset.</p>
                            </div>
                        </div>
                    ",
                    RelatedTopics = new() { { "dashboard", "Dashboard" }, { "assets", "Assets" }, { "depreciation", "Depreciation" } }
                },

                "maintenance" => new HelpTopic
                {
                    Title = "Understanding Maintenance Management",
                    Content = @"
                        <h2>What is Maintenance Management?</h2>
                        <p>Maintenance management helps you keep your assets running smoothly by scheduling regular upkeep, tracking repairs, and monitoring costs. Good maintenance extends asset life and reduces unexpected downtime.</p>
                        
                        <h3>Types of Maintenance</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dcfce7; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #16a34a; margin-top: 0;'>Preventive</h4>
                                <p>Scheduled maintenance to prevent failures before they happen. Like oil changes for your car.</p>
                            </div>
                            <div style='background: #fef3c7; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #d97706; margin-top: 0;'>Corrective</h4>
                                <p>Repairs done when something breaks or malfunctions. Fix it after it fails.</p>
                            </div>
                            <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #2563eb; margin-top: 0;'>Inspection</h4>
                                <p>Regular checks to identify potential issues before they become problems.</p>
                            </div>
                        </div>
                        
                        <h3>Maintenance Workflow</h3>
                        <div style='display: flex; gap: 0.5rem; flex-wrap: wrap; margin: 1rem 0;'>
                            <div style='flex: 1; min-width: 120px; background: #f1f5f9; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>1. Schedule</strong><br><small>Plan the work</small>
                            </div>
                            <div style='flex: 1; min-width: 120px; background: #dbeafe; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>2. In Progress</strong><br><small>Technician working</small>
                            </div>
                            <div style='flex: 1; min-width: 120px; background: #dcfce7; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>3. Complete</strong><br><small>Work finished</small>
                            </div>
                        </div>
                        
                        <h3>Cost Tracking</h3>
                        <p>For each maintenance event, you can track:</p>
                        <ul>
                            <li><strong>Labor Costs</strong> - Technician hours × hourly rate</li>
                            <li><strong>Parts Costs</strong> - Replacement parts used</li>
                            <li><strong>Materials Costs</strong> - Consumables like lubricants, filters</li>
                            <li><strong>Vendor Costs</strong> - External contractor charges</li>
                        </ul>
                        
                        <h3>Technicians</h3>
                        <p>Assign maintenance technicians to work orders. Track their specialties, hourly rates, and workload to optimize scheduling.</p>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "cip", "Capital Projects" } }
                },

                "cip" => new HelpTopic
                {
                    Title = "Understanding Capital Projects (CIP)",
                    Content = @"
                        <h2>What is CIP?</h2>
                        <p><strong>Construction-in-Progress (CIP)</strong> refers to capital projects that are being built or developed and will become fixed assets when completed. Think of building a new manufacturing line, renovating a facility, or installing major equipment.</p>
                        
                        <h3>CIP vs Regular Assets</h3>
                        <div style='display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #f1f5f9; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='margin-top: 0;'>Regular Assets</h4>
                                <ul style='margin: 0;'>
                                    <li>Already in use</li>
                                    <li>Depreciating</li>
                                    <li>Single cost recorded</li>
                                </ul>
                            </div>
                            <div style='background: #f3e8ff; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='margin-top: 0;'>CIP Projects</h4>
                                <ul style='margin: 0;'>
                                    <li>Under construction</li>
                                    <li>Not depreciating yet</li>
                                    <li>Costs accumulate over time</li>
                                </ul>
                            </div>
                        </div>
                        
                        <h3>Project Lifecycle</h3>
                        <div style='display: flex; gap: 0.5rem; flex-wrap: wrap; margin: 1rem 0;'>
                            <div style='flex: 1; min-width: 100px; background: #dbeafe; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>Planning</strong>
                            </div>
                            <div style='flex: 1; min-width: 100px; background: #fef3c7; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>In Progress</strong>
                            </div>
                            <div style='flex: 1; min-width: 100px; background: #dcfce7; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>Complete</strong>
                            </div>
                            <div style='flex: 1; min-width: 100px; background: #f3e8ff; padding: 1rem; border-radius: 8px; text-align: center;'>
                                <strong>Capitalized</strong>
                            </div>
                        </div>
                        
                        <h3>Cost Types</h3>
                        <p>CIP projects can track 12 different types of costs:</p>
                        <div style='display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.5rem; margin: 1rem 0;'>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Labor</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Materials</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Equipment</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Contractor</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Permits</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Engineering</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Inspection</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Freight</div>
                            <div style='background: #f8fafc; padding: 0.5rem; border-radius: 4px; font-size: 0.9rem;'>Other</div>
                        </div>
                        
                        <h3>Capitalization</h3>
                        <p>When the project is complete, you ""capitalize"" it - converting the accumulated CIP costs into a new fixed asset that can then be depreciated.</p>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "maintenance", "Maintenance" } }
                },

                "inventory" => new HelpTopic
                {
                    Title = "Understanding Inventory & Tracking",
                    Content = @"
                        <h2>What is Asset Inventory?</h2>
                        <p>Asset inventory is the process of physically verifying that your recorded assets actually exist and are in the right locations. It helps catch missing, moved, or ""ghost"" assets that exist only in records.</p>
                        
                        <h3>Why Do Inventory Counts?</h3>
                        <ul>
                            <li><strong>Accuracy</strong> - Ensure your records match reality</li>
                            <li><strong>Compliance</strong> - Required for audits and financial reporting</li>
                            <li><strong>Loss Prevention</strong> - Identify missing or stolen equipment</li>
                            <li><strong>Insurance</strong> - Verify coverage matches actual assets</li>
                        </ul>
                        
                        <h3>Inventory Process</h3>
                        <div style='display: flex; flex-direction: column; gap: 1rem; margin: 1rem 0;'>
                            <div style='display: flex; align-items: center; gap: 1rem;'>
                                <div style='width: 40px; height: 40px; background: #3b82f6; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold;'>1</div>
                                <div><strong>Create Inventory List</strong> - Define what assets to count (by location, type, etc.)</div>
                            </div>
                            <div style='display: flex; align-items: center; gap: 1rem;'>
                                <div style='width: 40px; height: 40px; background: #3b82f6; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold;'>2</div>
                                <div><strong>Scan Assets</strong> - Use barcode/QR scanning or manual entry to record found assets</div>
                            </div>
                            <div style='display: flex; align-items: center; gap: 1rem;'>
                                <div style='width: 40px; height: 40px; background: #3b82f6; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold;'>3</div>
                                <div><strong>Review Results</strong> - Compare scanned vs expected, identify discrepancies</div>
                            </div>
                            <div style='display: flex; align-items: center; gap: 1rem;'>
                                <div style='width: 40px; height: 40px; background: #3b82f6; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: bold;'>4</div>
                                <div><strong>Reconcile</strong> - Investigate and resolve differences</div>
                            </div>
                        </div>
                        
                        <h3>Tracking Results</h3>
                        <p>After scanning, you'll see:</p>
                        <ul>
                            <li><strong>Found</strong> - Assets that were scanned and verified</li>
                            <li><strong>Missing</strong> - Expected assets that weren't found</li>
                            <li><strong>Extra</strong> - Assets found that weren't expected in that location</li>
                        </ul>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "admin", "Administration" } }
                },

                "ai-assistant" => new HelpTopic
                {
                    Title = "Using the AI Assistant",
                    Content = @"
                        <h2>What is the AI Assistant?</h2>
                        <p>The AI Assistant lets you ask questions about your assets in plain English. Instead of navigating menus and running reports, just ask what you want to know!</p>
                        
                        <h3>What Can You Ask?</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dbeafe; padding: 1rem; border-radius: 8px;'>
                                <strong>Asset Questions</strong>
                                <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem;'>
                                    <li>""What's our total asset value?""</li>
                                    <li>""Show me assets at MISS location""</li>
                                    <li>""Which assets are fully depreciated?""</li>
                                </ul>
                            </div>
                            <div style='background: #dcfce7; padding: 1rem; border-radius: 8px;'>
                                <strong>Maintenance Questions</strong>
                                <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem;'>
                                    <li>""What maintenance is overdue?""</li>
                                    <li>""Show upcoming maintenance""</li>
                                    <li>""How much have we spent on repairs?""</li>
                                </ul>
                            </div>
                            <div style='background: #fef3c7; padding: 1rem; border-radius: 8px;'>
                                <strong>Project Questions</strong>
                                <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem;'>
                                    <li>""What CIP projects are active?""</li>
                                    <li>""Which projects are over budget?""</li>
                                    <li>""Show project spending summary""</li>
                                </ul>
                            </div>
                            <div style='background: #f3e8ff; padding: 1rem; border-radius: 8px;'>
                                <strong>Financial Questions</strong>
                                <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem;'>
                                    <li>""What's our depreciation this year?""</li>
                                    <li>""Show assets by manufacturer""</li>
                                    <li>""Top 10 most expensive assets""</li>
                                </ul>
                            </div>
                        </div>
                        
                        <h3>Tips for Good Questions</h3>
                        <ul>
                            <li><strong>Be specific</strong> - ""Assets at BRAM"" works better than ""show some stuff""</li>
                            <li><strong>Use natural language</strong> - Ask like you're talking to a colleague</li>
                            <li><strong>Click the links</strong> - AI responses include clickable links to jump directly to relevant pages</li>
                        </ul>
                        
                        <h3>How to Access</h3>
                        <p>Look for the <strong>AI Assistant</strong> button in the sidebar or use the chat icon in the bottom corner of any page.</p>
                    ",
                    RelatedTopics = new() { { "dashboard", "Dashboard" }, { "assets", "Assets" } }
                },

                "us-tax" => new HelpTopic
                {
                    Title = "Understanding US Tax Depreciation",
                    Content = @"
                        <h2>US Tax Depreciation Methods</h2>
                        <p>For US tax purposes, assets are depreciated using specific IRS rules. The main methods are MACRS, Section 179, and Bonus Depreciation.</p>
                        
                        <h3>MACRS (Modified Accelerated Cost Recovery System)</h3>
                        <p>The primary depreciation method for US tax purposes. Assets are grouped into ""property classes"" with set recovery periods.</p>
                        
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Property Class</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Recovery Period</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Examples</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>3-Year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>3 years</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Tractors, racehorses</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>5-Year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>5 years</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Computers, vehicles, office equipment</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>7-Year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>7 years</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Office furniture, manufacturing equipment</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>15-Year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>15 years</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Land improvements, fencing</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>39-Year</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>39 years</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Commercial buildings</td>
                            </tr>
                        </table>
                        
                        <h3>Section 179 Expensing</h3>
                        <div style='background: #dcfce7; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='margin-top: 0; color: #16a34a;'>Immediate Deduction</h4>
                            <p>Section 179 lets you deduct the <strong>full cost</strong> of qualifying equipment in the year you buy it, instead of depreciating over time.</p>
                            <p><strong>Limits apply:</strong> There's an annual maximum and phase-out thresholds based on total purchases.</p>
                        </div>
                        
                        <h3>Bonus Depreciation</h3>
                        <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='margin-top: 0; color: #2563eb;'>Additional First-Year Deduction</h4>
                            <p>Allows an extra percentage deduction in the first year for new assets. The rate has been phasing down:</p>
                            <ul style='margin: 0;'>
                                <li>2023: 80%</li>
                                <li>2024: 60%</li>
                                <li>2025: 40%</li>
                                <li>2026: 20%</li>
                            </ul>
                        </div>
                        
                        <h3>Half-Year Convention</h3>
                        <p>Similar to Canadian rules, the US uses a ""half-year convention"" - assets placed in service are treated as if used for half the year, regardless of actual purchase date.</p>
                    ",
                    RelatedTopics = new() { { "depreciation", "Depreciation" }, { "cca", "CCA (Canadian)" }, { "books", "Books" } }
                },

                "master-files" => new HelpTopic
                {
                    Title = "Master Files",
                    Content = @"
                        <h2>Enterprise Master Files</h2>
                        <p>Master Files provide the foundational reference data for tracking fixed assets across your organization with full dimensional reporting capabilities.</p>
                        
                        <h3>GL Account Structure</h3>
                        <p>The system includes 82+ pre-configured GL accounts organized by function:</p>
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Range</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Category</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Examples</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>1000s</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Cash & Receivables</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Operating Cash, A/R, Petty Cash</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>1200s</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>MRO Inventory</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Spare Parts, Consumables, Safety Equipment</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>1500-1900s</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Fixed Assets</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Buildings, Machinery, Vehicles, Technology, Tooling</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>1940-IC</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Intercompany</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Due From/To Subsidiaries, Investment in Subsidiaries</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>1950s</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Accumulated Depreciation</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Contra-asset accounts by category</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>3500-IC</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Intercompany Eliminations</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Consolidation entries, Currency Translation</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>6000s</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Operating Expenses</td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Depreciation, Maintenance, Repairs, Calibration</td>
                            </tr>
                        </table>
                        
                        <h3>Intercompany Accounts (Multi-Company Mode)</h3>
                        <div style='background: #f3e8ff; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='margin-top: 0; color: #7c3aed;'>For Holding Company Structures</h4>
                            <p>When running in Multi-Company mode, 15 intercompany accounts are available:</p>
                            <ul style='margin: 0.5rem 0;'>
                                <li><strong>Due From Subsidiaries</strong> - Receivables from subsidiary companies</li>
                                <li><strong>Due To Parent/Affiliates</strong> - Payables to parent or sister companies</li>
                                <li><strong>Investment in Subsidiaries</strong> - Parent's equity stake in operating companies</li>
                                <li><strong>Intercompany Eliminations</strong> - For consolidated financial statements</li>
                                <li><strong>Currency Translation Adjustment</strong> - For multi-currency subsidiaries</li>
                            </ul>
                        </div>
                        
                        <h3>Dimensional Tracking</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='margin-top: 0; color: #2563eb;'>Cost Centers</h4>
                                <p>Track assets across plants, buildings, production lines, and work cells with hierarchical location structure.</p>
                            </div>
                            <div style='background: #dcfce7; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='margin-top: 0; color: #16a34a;'>Departments</h4>
                                <p>Allocate costs by department (Maintenance, Production, Quality, Engineering, etc.) for accurate cost accounting.</p>
                            </div>
                            <div style='background: #fef3c7; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='margin-top: 0; color: #d97706;'>Asset Categories</h4>
                                <p>Group assets by type with default MACRS classes, useful lives, and automatic GL account mapping.</p>
                            </div>
                        </div>
                        
                        <h3>GL Account Types</h3>
                        <ul>
                            <li><strong>Asset</strong> - What you own (normal debit balance)</li>
                            <li><strong>Liability</strong> - What you owe (normal credit balance)</li>
                            <li><strong>Equity</strong> - Owner's stake (normal credit balance)</li>
                            <li><strong>Revenue</strong> - Income earned (normal credit balance)</li>
                            <li><strong>Expense</strong> - Costs incurred (normal debit balance)</li>
                            <li><strong>Contra-Asset</strong> - Offsets assets like Accumulated Depreciation (credit balance)</li>
                        </ul>
                    ",
                    RelatedTopics = new() { { "admin", "Administration" }, { "company-settings", "Company Settings" }, { "depreciation", "Depreciation" } }
                },

                "admin" => new HelpTopic
                {
                    Title = "Administration & Settings",
                    Content = @"
                        <h2>Enterprise Administration</h2>
                        <p>The Admin section lets you configure your system, manage users, and control organization-wide settings.</p>
                        
                        <h3>Admin Modules</h3>
                        <div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #3b82f6;'>
                                <h4 style='margin-top: 0;'>Company Settings</h4>
                                <p>Configure company name, address, tax IDs, fiscal year, and branding.</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #22c55e;'>
                                <h4 style='margin-top: 0;'>User Management</h4>
                                <p>Add, edit, and deactivate users. Assign roles (Admin, Accountant, Viewer).</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #f59e0b;'>
                                <h4 style='margin-top: 0;'>Technicians</h4>
                                <p>Manage maintenance technicians with specialties and hourly rates.</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #a855f7;'>
                                <h4 style='margin-top: 0;'>Project Managers</h4>
                                <p>Manage CIP project managers and their assignments.</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #ec4899;'>
                                <h4 style='margin-top: 0;'>Exchange Rates</h4>
                                <p>Set currency exchange rates for multi-currency operations.</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #64748b;'>
                                <h4 style='margin-top: 0;'>Audit Log</h4>
                                <p>View all system changes with timestamps and user information.</p>
                            </div>
                            <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; border-left: 4px solid #0ea5e9;'>
                                <h4 style='margin-top: 0;'>Chart of Accounts</h4>
                                <p>GL Accounts, Cost Centers, Departments, and Asset Categories for dimensional tracking.</p>
                            </div>
                        </div>
                        
                        <h3>User Roles</h3>
                        <table style='width: 100%; border-collapse: collapse; margin: 1rem 0;'>
                            <tr style='background: #f1f5f9;'>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Role</th>
                                <th style='padding: 0.75rem; text-align: left; border: 1px solid #e2e8f0;'>Permissions</th>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Admin</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Full access to all features including user management and settings</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Accountant</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Create, edit, run depreciation, manage assets - no admin access</td>
                            </tr>
                            <tr>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'><strong>Viewer</strong></td>
                                <td style='padding: 0.75rem; border: 1px solid #e2e8f0;'>Read-only access to view assets and reports</td>
                            </tr>
                        </table>
                        
                        <h3>Period Locking</h3>
                        <p>Lock accounting periods to prevent changes to historical data. Once a period is locked, no new transactions can be posted to that month.</p>
                    ",
                    RelatedTopics = new() { { "dashboard", "Dashboard" }, { "glossary", "Glossary" } }
                },

                "company-settings" => new HelpTopic
                {
                    Title = "Company Settings & Structure",
                    Content = @"
                        <h2>Company Settings</h2>
                        <p>The Company Settings page lets you configure your organization's details, fiscal settings, and company structure.</p>
                        
                        <h3>Company Structure</h3>
                        <p>Choose how your organization is set up:</p>
                        
                        <div style='display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; margin: 1rem 0;'>
                            <div style='background: #dbeafe; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #1d4ed8; margin-top: 0;'>Single Company Mode</h4>
                                <p>For standalone manufacturing businesses with one legal entity.</p>
                                <ul style='margin: 0;'>
                                    <li>Simpler setup and navigation</li>
                                    <li>No holding/subsidiary complexity</li>
                                    <li>Standard GL accounts</li>
                                </ul>
                            </div>
                            <div style='background: #f3e8ff; padding: 1.5rem; border-radius: 8px;'>
                                <h4 style='color: #7c3aed; margin-top: 0;'>Multi-Company / Holding Mode</h4>
                                <p>For holding companies with operating subsidiaries.</p>
                                <ul style='margin: 0;'>
                                    <li>Parent-child company relationships</li>
                                    <li>Intercompany GL accounts</li>
                                    <li>Multi-currency support</li>
                                    <li>Consolidated reporting</li>
                                </ul>
                            </div>
                        </div>
                        
                        <h3>Key Settings</h3>
                        <ul>
                            <li><strong>Company Information</strong> - Name, address, contact details</li>
                            <li><strong>Tax Registration</strong> - Tax ID, EIN, GST/HST numbers</li>
                            <li><strong>Fiscal Settings</strong> - Currency, accounting period type, fiscal year start</li>
                            <li><strong>Depreciation Defaults</strong> - Default method and convention for new assets</li>
                            <li><strong>Approval Settings</strong> - Thresholds for disposals and transfers</li>
                        </ul>
                        
                        <h3>Changing Company Structure</h3>
                        <p>You can switch between Single Company and Multi-Company modes at any time. When enabling Multi-Company mode:</p>
                        <ul>
                            <li>The Corporate Structure card appears showing your company hierarchy</li>
                            <li>Intercompany GL accounts become available</li>
                            <li>You can add subsidiary companies under your holding company</li>
                        </ul>
                    ",
                    RelatedTopics = new() { { "admin", "Administration" }, { "master-files", "Master Files" } }
                },

                // PR #107 / B-23: stub topic surfaced from the asset detail page
                // empty-state badge when no IoT/OEE/health data has been captured.
                // Sets honest expectations: "we have the schema; you configure
                // the gateway." Replaces the previous behavior of displaying
                // hardcoded "-" placeholders that implied the data was tracked
                // when it actually wasn't.
                "iot-setup" => new HelpTopic
                {
                    Title = "Connecting IoT Data to Your Assets",
                    Content = @"
                        <h2>Reliability data is captured per-asset, not faked</h2>
                        <p>The asset detail page shows IoT, OEE, and health-monitoring sections only when there is actual data behind them. If you're seeing the empty-state banner that pointed you here, this asset hasn't been wired up to the data sources yet.</p>

                        <h3>Three sources we read from</h3>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #3b82f6; margin-top: 0;'>1. IoT gateway (real-time sensors)</h4>
                            <p>For live temperature, vibration, pressure, and connection-status readings. The fields the gateway writes to are exposed via the asset's <code>IoTEndpointUrl</code>, <code>IoTProtocol</code>, and <code>DataHistorianTag</code> — configurable in this app's <strong>Edit Asset → IoT</strong> tab. Once your gateway is publishing to that endpoint, the Current Sensor Readings and Connection Status panels will populate automatically on the asset detail page.</p>
                        </div>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #22c55e; margin-top: 0;'>2. MES / SCADA system (OEE)</h4>
                            <p>For Current Availability, Performance, Quality, and OEE %. Your MES exposes per-shift counters and posts them to the asset's <code>SCADATag</code>. The Current OEE block appears on the asset detail page as soon as any of those four numbers has a recent value.</p>
                        </div>
                        <div style='background: #f8fafc; padding: 1.5rem; border-radius: 8px; margin: 1rem 0;'>
                            <h4 style='color: #f59e0b; margin-top: 0;'>3. Predictive health scoring (optional)</h4>
                            <p>If you have a predictive-maintenance product feeding health scores back, it can stamp <code>HealthScore</code>, <code>PredictedFailureDate</code>, etc. and they'll surface on the asset detail. Without a feed, those fields stay hidden — by design.</p>
                        </div>

                        <h3>Configuring an asset for IoT</h3>
                        <ol>
                            <li>Open the asset's <strong>Edit</strong> mode.</li>
                            <li>Go to the <strong>IoT</strong> sub-tab.</li>
                            <li>Check <em>IoT Enabled</em> and fill in <em>Device ID</em>, <em>Gateway ID</em>, <em>Protocol</em>, and <em>Endpoint URL</em>.</li>
                            <li>Save. The asset is now flagged as in-scope for your gateway.</li>
                            <li>Configure your gateway to publish to the endpoint. Once readings arrive, the asset detail page's IoT panels appear automatically.</li>
                        </ol>

                        <h3>What if I don't have an IoT gateway?</h3>
                        <p>Most of CherryAI EAM works fine without IoT — work orders, depreciation, financials, capital projects, P2P, etc. all function. The IoT/OEE/Health fields exist for customers who plug those data sources in. If you don't, the asset detail page simply won't show those sections — which is what you want, not a wall of dashes.</p>
                    ",
                    RelatedTopics = new() { { "assets", "Assets" }, { "maintenance", "Maintenance" } }
                },

                _ => null
            };
        }

        private class HelpTopic
        {
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
            public Dictionary<string, string> RelatedTopics { get; set; } = new();
        }
    }
}
