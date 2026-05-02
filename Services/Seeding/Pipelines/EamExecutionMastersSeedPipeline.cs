using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Seeding.Pipelines
{
    public class EamExecutionMastersSeedPipeline : ISeedPipeline
    {
        public string Name => "EamExecutionMastersSeed";
        public string Version => "3.0.0";
        public string Description => "EAM execution masters: Technicians (enriched), Certifications, Skills, PM Templates, and work order configuration";
        public bool IsDevOnly => false;

        private readonly List<ISeedStep> _steps;
        public IReadOnlyList<ISeedStep> Steps => _steps;

        public EamExecutionMastersSeedPipeline(AppDbContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<EamExecutionMastersSeedPipeline>();
            _steps = new List<ISeedStep>
            {
                new TechniciansSeedStep(context, logger),
                new TechnicianSupervisorLinkageStep(context, logger),
                new TechnicianCertificationsSeedStep(context, logger),
                new TechnicianSkillsSeedStep(context, logger)
            };
        }
    }

    #region Technicians
    public class TechniciansSeedStep : BaseSeedStep<Technician>
    {
        public override string StepName => "Technicians";
        public override string DomainName => "Technicians";
        public override string NaturalKeyDescription => "Name";

        public TechniciansSeedStep(AppDbContext context, ILogger logger) : base(context, logger) { }

        protected override IEnumerable<Technician> GetSeedData() => new[]
        {
            new Technician
            {
                Name = "David Chen",
                EmployeeId = "EMP-2019-0103",
                Title = "Senior CNC Specialist",
                Email = "dchen@absmachining.com",
                Phone = "810-555-1003",
                Specialty = "5-Axis CNC Programming & Setup",
                PrimaryCraft = "CNC Machinist",
                SecondaryCraft = "Millwright",
                ProficiencyLevel = 3,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(14, 30),
                HourlyRate = 52.00m,
                OvertimeRate = 78.00m,
                HireDate = new DateTime(2019, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Lisa Chen",
                EmergencyContactPhone = "810-555-2003",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "Red Seal CNC Machinist. 5-Axis CNC Programming & Setup specialist."
            },
            new Technician
            {
                Name = "Marcus Williams",
                EmployeeId = "EMP-2017-0087",
                Title = "Lead Electrician",
                Email = "mwilliams@absmachining.com",
                Phone = "810-555-1004",
                Specialty = "Industrial Electrical & PLC Programming",
                PrimaryCraft = "Electrician",
                SecondaryCraft = "Instrumentation",
                ProficiencyLevel = 4,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(14, 30),
                HourlyRate = 58.00m,
                OvertimeRate = 87.00m,
                HireDate = new DateTime(2017, 8, 22, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Angela Williams",
                EmergencyContactPhone = "810-555-2004",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "309A Industrial Electrician. Lead electrician for plant floor."
            },
            new Technician
            {
                Name = "Sarah Kowalski",
                EmployeeId = "EMP-2020-0156",
                Title = "Millwright",
                Email = "skowalski@absmachining.com",
                Phone = "810-555-1005",
                Specialty = "Heavy Equipment Alignment & Precision Assembly",
                PrimaryCraft = "Millwright",
                SecondaryCraft = "Welder",
                ProficiencyLevel = 2,
                ShiftPattern = "Afternoon",
                ShiftStart = new TimeOnly(14, 30),
                ShiftEnd = new TimeOnly(23, 0),
                HourlyRate = 48.00m,
                OvertimeRate = 72.00m,
                HireDate = new DateTime(2020, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Mark Kowalski",
                EmergencyContactPhone = "810-555-2005",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "Red Seal Millwright. Heavy Equipment Alignment & Precision Assembly."
            },
            new Technician
            {
                Name = "James Okafor",
                EmployeeId = "EMP-2021-0178",
                Title = "Hydraulics Technician",
                Email = "jokafor@absmachining.com",
                Phone = "810-555-1006",
                Specialty = "High-Pressure Hydraulic Systems",
                PrimaryCraft = "Hydraulics",
                SecondaryCraft = "Pneumatics",
                ProficiencyLevel = 2,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(14, 30),
                HourlyRate = 46.00m,
                OvertimeRate = 69.00m,
                HireDate = new DateTime(2021, 5, 3, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Grace Okafor",
                EmergencyContactPhone = "810-555-2006",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "Hydraulic Systems Certified. High-Pressure Hydraulic Systems specialist."
            },
            new Technician
            {
                Name = "Maria Santos",
                EmployeeId = "EMP-2018-0094",
                Title = "Senior Welder/Fabricator",
                Email = "msantos@absmachining.com",
                Phone = "810-555-1007",
                Specialty = "TIG/MIG Welding, CWB Certified",
                PrimaryCraft = "Welder",
                SecondaryCraft = "Fabricator",
                ProficiencyLevel = 3,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(14, 30),
                HourlyRate = 50.00m,
                OvertimeRate = 75.00m,
                HireDate = new DateTime(2018, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Carlos Santos",
                EmergencyContactPhone = "810-555-2007",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "CWB Certified Welder. TIG/MIG Welding specialist."
            },
            new Technician
            {
                Name = "Robert Tremblay",
                EmployeeId = "EMP-2022-0201",
                Title = "Apprentice Millwright",
                Email = "rtremblay@absmachining.com",
                Phone = "810-555-1008",
                Specialty = "2nd Year Apprentice — Mechanical Systems",
                PrimaryCraft = "Millwright",
                ProficiencyLevel = 1,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(14, 30),
                HourlyRate = 32.00m,
                OvertimeRate = 48.00m,
                HireDate = new DateTime(2022, 9, 5, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Jean Tremblay",
                EmergencyContactPhone = "810-555-2008",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "2nd Year Apprentice — Mechanical Systems."
            },
            new Technician
            {
                Name = "Aisha Patel",
                EmployeeId = "EMP-2016-0062",
                Title = "Maintenance Supervisor",
                Email = "apatel@absmachining.com",
                Phone = "810-555-1009",
                Specialty = "Maintenance Planning & Reliability Engineering",
                PrimaryCraft = "Millwright",
                SecondaryCraft = "Electrician",
                ProficiencyLevel = 4,
                ShiftPattern = "Day",
                ShiftStart = new TimeOnly(6, 0),
                ShiftEnd = new TimeOnly(15, 0),
                HourlyRate = 62.00m,
                OvertimeRate = 93.00m,
                HireDate = new DateTime(2016, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Raj Patel",
                EmergencyContactPhone = "810-555-2009",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "CMRP certified. Maintenance Planning & Reliability Engineering lead."
            },
            new Technician
            {
                Name = "Kevin Nguyen",
                EmployeeId = "EMP-2023-0234",
                Title = "CNC Operator/Technician",
                Email = "knguyen@absmachining.com",
                Phone = "810-555-1010",
                Specialty = "Haas & Mazak CNC Operations",
                PrimaryCraft = "CNC Machinist",
                ProficiencyLevel = 2,
                ShiftPattern = "Night",
                ShiftStart = new TimeOnly(23, 0),
                ShiftEnd = new TimeOnly(7, 0),
                HourlyRate = 44.00m,
                OvertimeRate = 66.00m,
                HireDate = new DateTime(2023, 2, 14, 0, 0, 0, DateTimeKind.Utc),
                EmergencyContactName = "Linh Nguyen",
                EmergencyContactPhone = "810-555-2010",
                CompanyId = 2,
                SiteId = 1,
                TenantId = 1,
                Active = true,
                Notes = "Haas & Mazak CNC Operations. Night shift CNC operator."
            }
        };

        protected override async Task<Technician?> FindByNaturalKeyAsync(Technician item, CancellationToken ct)
            => await Context.Technicians.FirstOrDefaultAsync(x => x.Name.ToLower() == item.Name.ToLower(), ct);
        protected override string GetNaturalKeyValue(Technician item) => item.Name;
        protected override bool ShouldUpdate(Technician existing, Technician incoming)
            => !StringEquals(existing.EmployeeId, incoming.EmployeeId)
               || !StringEquals(existing.Title, incoming.Title)
               || !StringEquals(existing.Specialty, incoming.Specialty)
               || !StringEquals(existing.PrimaryCraft, incoming.PrimaryCraft)
               || !StringEquals(existing.Phone, incoming.Phone)
               || !StringEquals(existing.Email, incoming.Email)
               || existing.HourlyRate != incoming.HourlyRate
               || existing.ProficiencyLevel != incoming.ProficiencyLevel
               || existing.CompanyId != incoming.CompanyId
               || existing.SiteId != incoming.SiteId;
        protected override void UpdateEntity(Technician existing, Technician incoming)
        {
            existing.EmployeeId = incoming.EmployeeId;
            existing.Title = incoming.Title;
            existing.Specialty = incoming.Specialty;
            existing.Phone = incoming.Phone;
            existing.Email = incoming.Email;
            existing.PrimaryCraft = incoming.PrimaryCraft;
            existing.SecondaryCraft = incoming.SecondaryCraft;
            existing.ProficiencyLevel = incoming.ProficiencyLevel;
            existing.ShiftPattern = incoming.ShiftPattern;
            existing.ShiftStart = incoming.ShiftStart;
            existing.ShiftEnd = incoming.ShiftEnd;
            existing.HourlyRate = incoming.HourlyRate;
            existing.OvertimeRate = incoming.OvertimeRate;
            existing.DoubleTimeRate = incoming.DoubleTimeRate;
            existing.HireDate = incoming.HireDate;
            existing.EmergencyContactName = incoming.EmergencyContactName;
            existing.EmergencyContactPhone = incoming.EmergencyContactPhone;
            existing.CompanyId = incoming.CompanyId;
            existing.SiteId = incoming.SiteId;
            existing.TenantId = incoming.TenantId;
            existing.Notes = incoming.Notes;
        }
    }
    #endregion

    #region TechnicianSupervisorLinkage
    public class TechnicianSupervisorLinkageStep : ISeedStep
    {
        public string StepName => "TechnicianSupervisorLinkage";
        public string DomainName => "Technicians";
        public string NaturalKeyDescription => "EmployeeId→SupervisorEmployeeId";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public TechnicianSupervisorLinkageStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new SeedStepResult
            {
                StepName = StepName,
                DomainName = "Technicians",
                StartTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t, cancellationToken);

                var links = new Dictionary<string, string>
                {
                    { "EMP-2022-0201", "EMP-2016-0062" },
                    { "EMP-2023-0234", "EMP-2019-0103" }
                };

                foreach (var (empId, supId) in links)
                {
                    if (techLookup.TryGetValue(empId, out var tech) && techLookup.TryGetValue(supId, out var sup))
                    {
                        if (tech.SupervisorTechnicianId != sup.Id)
                        {
                            tech.SupervisorTechnicianId = sup.Id;
                            result.Updated++;
                        }
                        else
                        {
                            result.Skipped++;
                        }
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Step execution error: {ex.Message}");
                _logger.LogError(ex, "Error in seed step {Step}", StepName);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default)
        {
            var preview = new PreviewStepResult
            {
                StepName = StepName,
                DomainName = "Technicians",
                PreviewTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t, cancellationToken);

                var links = new Dictionary<string, string>
                {
                    { "EMP-2022-0201", "EMP-2016-0062" },
                    { "EMP-2023-0234", "EMP-2019-0103" }
                };

                foreach (var (empId, supId) in links)
                {
                    if (techLookup.TryGetValue(empId, out var tech) && techLookup.TryGetValue(supId, out var sup))
                    {
                        if (tech.SupervisorTechnicianId != sup.Id) preview.WouldCreate++;
                        else preview.WouldSkip++;
                    }
                }

                preview.TotalInSeedData = links.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in preview step {Step}", StepName);
            }

            return preview;
        }
    }
    #endregion

    #region TechnicianCertifications
    public class TechnicianCertificationsSeedStep : ISeedStep
    {
        public string StepName => "TechnicianCertifications";
        public string DomainName => "TechnicianCertifications";
        public string NaturalKeyDescription => "TechnicianId+Name";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public TechnicianCertificationsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new SeedStepResult
            {
                StepName = StepName,
                DomainName = "TechnicianCertifications",
                StartTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t.Id, cancellationToken);

                var certData = GetCertificationData(techLookup);
                foreach (var cert in certData)
                {
                    var exists = await _context.TechnicianCertifications
                        .AnyAsync(c => c.TechnicianId == cert.TechnicianId && c.Name == cert.Name, cancellationToken);
                    if (!exists)
                    {
                        _context.TechnicianCertifications.Add(cert);
                        result.Inserted++;
                    }
                    else
                    {
                        result.Skipped++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Step execution error: {ex.Message}");
                _logger.LogError(ex, "Error in seed step {Step}", StepName);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default)
        {
            var preview = new PreviewStepResult
            {
                StepName = StepName,
                DomainName = "TechnicianCertifications",
                PreviewTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t.Id, cancellationToken);

                var certData = GetCertificationData(techLookup);
                preview.TotalInSeedData = certData.Count;
                foreach (var cert in certData)
                {
                    var exists = await _context.TechnicianCertifications
                        .AnyAsync(c => c.TechnicianId == cert.TechnicianId && c.Name == cert.Name, cancellationToken);
                    if (exists) preview.WouldSkip++;
                    else preview.WouldCreate++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in preview step {Step}", StepName);
            }

            return preview;
        }

        private List<TechnicianCertification> GetCertificationData(Dictionary<string, int> techLookup)
        {
            var certs = new List<TechnicianCertification>();

            if (techLookup.TryGetValue("EMP-2019-0103", out var davidId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = davidId, Name = "Red Seal CNC Machinist", CertificateNumber = "RS-ON-2021-4582", IssuingAuthority = "Ontario College of Trades", IssueDate = new DateTime(2021, 6, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = davidId, Name = "Fanuc Robotics Certified", CertificateNumber = "FANUC-2023-1187", IssuingAuthority = "FANUC America", IssueDate = new DateTime(2023, 3, 20, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = davidId, Name = "WHMIS 2015", CertificateNumber = "W-2025-8834", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = davidId, Name = "Overhead Crane Operator", CertificateNumber = "CR-ON-2022-5501", IssuingAuthority = "Ontario Crane Association", IssueDate = new DateTime(2022, 8, 15, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2017-0087", out var marcusId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = marcusId, Name = "309A Industrial Electrician License", CertificateNumber = "309A-ON-2018-2291", IssuingAuthority = "Ontario College of Trades", IssueDate = new DateTime(2018, 9, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = marcusId, Name = "Allen-Bradley PLC Certified", CertificateNumber = "AB-PLC-2024-0887", IssuingAuthority = "Rockwell Automation", IssueDate = new DateTime(2024, 5, 12, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2027, 5, 12, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = marcusId, Name = "Arc Flash Safety (NFPA 70E)", CertificateNumber = "NFPA70E-2024-3341", IssuingAuthority = "NFPA", IssueDate = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = marcusId, Name = "WHMIS 2015", CertificateNumber = "W-2024-7761", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2020-0156", out var sarahId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = sarahId, Name = "Red Seal Millwright (Industrial Mechanic)", CertificateNumber = "RS-ON-2020-3887", IssuingAuthority = "Ontario College of Trades", IssueDate = new DateTime(2020, 4, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = sarahId, Name = "Rigging & Hoisting", CertificateNumber = "RIG-2023-4412", IssuingAuthority = "Lift Institute", IssueDate = new DateTime(2023, 2, 10, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = sarahId, Name = "WHMIS 2015", CertificateNumber = "W-2025-9902", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2021-0178", out var jamesId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = jamesId, Name = "Hydraulic Systems Certified", CertificateNumber = "HYD-2022-0934", IssuingAuthority = "International Fluid Power Society", IssueDate = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = jamesId, Name = "WHMIS 2015", CertificateNumber = "W-2025-4456", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2018-0094", out var mariaId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = mariaId, Name = "CWB Certified Welder (W47.1)", CertificateNumber = "CWB-W47-2019-6612", IssuingAuthority = "Canadian Welding Bureau", IssueDate = new DateTime(2019, 7, 20, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 7, 20, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = mariaId, Name = "CWB Welding Supervisor", CertificateNumber = "CWB-SUP-2022-1198", IssuingAuthority = "Canadian Welding Bureau", IssueDate = new DateTime(2022, 3, 15, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = mariaId, Name = "WHMIS 2015", CertificateNumber = "W-2025-1034", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2022-0201", out var robertId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = robertId, Name = "WHMIS 2015", CertificateNumber = "W-2025-3387", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = robertId, Name = "First Aid & CPR", CertificateNumber = "FA-2024-8891", IssuingAuthority = "Red Cross", IssueDate = new DateTime(2024, 9, 15, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2027, 9, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2016-0062", out var aishaId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = aishaId, Name = "Red Seal Millwright", CertificateNumber = "RS-ON-2016-2104", IssuingAuthority = "Ontario College of Trades", IssueDate = new DateTime(2016, 9, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = aishaId, Name = "309A Industrial Electrician License", CertificateNumber = "309A-ON-2019-5578", IssuingAuthority = "Ontario College of Trades", IssueDate = new DateTime(2019, 11, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = aishaId, Name = "CMRP (Certified Maintenance & Reliability Professional)", CertificateNumber = "CMRP-2023-0445", IssuingAuthority = "SMRP", IssueDate = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = aishaId, Name = "WHMIS 2015", CertificateNumber = "W-2024-6623", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2023-0234", out var kevinId))
            {
                certs.Add(new TechnicianCertification { TechnicianId = kevinId, Name = "WHMIS 2015", CertificateNumber = "W-2025-5578", IssuingAuthority = "Canadian Centre for OHS", IssueDate = new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), IsRequired = true, TenantId = 1 });
                certs.Add(new TechnicianCertification { TechnicianId = kevinId, Name = "Haas CNC Operator Certification", CertificateNumber = "HAAS-2023-2201", IssuingAuthority = "Haas Automation", IssueDate = new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc), ExpirationDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), IsRequired = false, TenantId = 1 });
            }

            return certs;
        }
    }
    #endregion

    #region TechnicianSkills
    public class TechnicianSkillsSeedStep : ISeedStep
    {
        public string StepName => "TechnicianSkills";
        public string DomainName => "TechnicianSkills";
        public string NaturalKeyDescription => "TechnicianId+SkillName";

        private readonly AppDbContext _context;
        private readonly ILogger _logger;

        public TechnicianSkillsSeedStep(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SeedStepResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new SeedStepResult
            {
                StepName = StepName,
                DomainName = "TechnicianSkills",
                StartTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t.Id, cancellationToken);

                var skillData = GetSkillData(techLookup);
                foreach (var skill in skillData)
                {
                    var exists = await _context.TechnicianSkills
                        .AnyAsync(s => s.TechnicianId == skill.TechnicianId && s.SkillName == skill.SkillName, cancellationToken);
                    if (!exists)
                    {
                        _context.TechnicianSkills.Add(skill);
                        result.Inserted++;
                    }
                    else
                    {
                        result.Skipped++;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Step execution error: {ex.Message}");
                _logger.LogError(ex, "Error in seed step {Step}", StepName);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<PreviewStepResult> PreviewAsync(CancellationToken cancellationToken = default)
        {
            var preview = new PreviewStepResult
            {
                StepName = StepName,
                DomainName = "TechnicianSkills",
                PreviewTime = DateTime.UtcNow
            };

            try
            {
                var techLookup = await _context.Technicians
                    .Where(t => t.EmployeeId != null)
                    .ToDictionaryAsync(t => t.EmployeeId!, t => t.Id, cancellationToken);

                var skillData = GetSkillData(techLookup);
                preview.TotalInSeedData = skillData.Count;
                foreach (var skill in skillData)
                {
                    var exists = await _context.TechnicianSkills
                        .AnyAsync(s => s.TechnicianId == skill.TechnicianId && s.SkillName == skill.SkillName, cancellationToken);
                    if (exists) preview.WouldSkip++;
                    else preview.WouldCreate++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in preview step {Step}", StepName);
            }

            return preview;
        }

        private List<TechnicianSkill> GetSkillData(Dictionary<string, int> techLookup)
        {
            var skills = new List<TechnicianSkill>();

            if (techLookup.TryGetValue("EMP-2019-0103", out var davidId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "5-Axis CNC Programming", Category = "CNC", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "Fanuc CNC Controls", Category = "CNC", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "Haas CNC Controls", Category = "CNC", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "Mazak CNC Controls", Category = "CNC", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "Precision Measurement & GD&T", Category = "Quality", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "CAM Software (MasterCAM)", Category = "CNC", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = davidId, SkillName = "Machine Alignment & Leveling", Category = "Mechanical", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2017-0087", out var marcusId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "Industrial Motor Controls & VFDs", Category = "Electrical", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "PLC Programming (Allen-Bradley)", Category = "Electrical", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "PLC Programming (Siemens)", Category = "Electrical", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "High Voltage Systems (600V)", Category = "Electrical", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "Conduit Bending & Installation", Category = "Electrical", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "HMI/SCADA Configuration", Category = "Controls", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = marcusId, SkillName = "Arc Flash Analysis", Category = "Safety", ProficiencyLevel = 4, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2020-0156", out var sarahId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "Precision Shaft Alignment (laser)", Category = "Mechanical", ProficiencyLevel = 5, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "Bearing Installation & Analysis", Category = "Mechanical", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "Pump Repair & Rebuild", Category = "Mechanical", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "Rigging & Heavy Lifting", Category = "Mechanical", ProficiencyLevel = 4, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "Blueprint Reading", Category = "General", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = sarahId, SkillName = "MIG Welding", Category = "Welding", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2021-0178", out var jamesId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = jamesId, SkillName = "Hydraulic System Design", Category = "Hydraulics", ProficiencyLevel = 4, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = jamesId, SkillName = "Hydraulic Troubleshooting", Category = "Hydraulics", ProficiencyLevel = 5, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = jamesId, SkillName = "Pneumatic Systems", Category = "Pneumatics", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = jamesId, SkillName = "Hose & Fitting Assembly", Category = "Hydraulics", ProficiencyLevel = 5, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = jamesId, SkillName = "Contamination Analysis", Category = "Hydraulics", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2018-0094", out var mariaId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "TIG Welding (GTAW)", Category = "Welding", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "MIG Welding (GMAW)", Category = "Welding", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "Stick Welding (SMAW)", Category = "Welding", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "Structural Steel Fabrication", Category = "Fabrication", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "Blueprint Reading & Layout", Category = "General", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = mariaId, SkillName = "Plasma & Oxy-Fuel Cutting", Category = "Fabrication", ProficiencyLevel = 5, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2022-0201", out var robertId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = robertId, SkillName = "Basic Hand Tools", Category = "General", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = robertId, SkillName = "Blueprint Reading", Category = "General", ProficiencyLevel = 2, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = robertId, SkillName = "Bearing Replacement", Category = "Mechanical", ProficiencyLevel = 2, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = robertId, SkillName = "Basic Welding", Category = "Welding", ProficiencyLevel = 1, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2016-0062", out var aishaId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Maintenance Planning & Scheduling", Category = "Management", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Reliability Centered Maintenance", Category = "Management", ProficiencyLevel = 5, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Root Cause Analysis", Category = "Management", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "CMMS Administration", Category = "Management", ProficiencyLevel = 5, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Shaft Alignment", Category = "Mechanical", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Electrical Troubleshooting", Category = "Electrical", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = aishaId, SkillName = "Budget & Cost Management", Category = "Management", ProficiencyLevel = 4, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            if (techLookup.TryGetValue("EMP-2023-0234", out var kevinId))
            {
                skills.Add(new TechnicianSkill { TechnicianId = kevinId, SkillName = "Haas CNC Operations", Category = "CNC", ProficiencyLevel = 4, IsCertified = true, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = kevinId, SkillName = "Mazak CNC Operations", Category = "CNC", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = kevinId, SkillName = "Basic G-Code Programming", Category = "CNC", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = kevinId, SkillName = "Precision Measurement", Category = "Quality", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
                skills.Add(new TechnicianSkill { TechnicianId = kevinId, SkillName = "Machine Coolant Management", Category = "CNC", ProficiencyLevel = 3, IsCertified = false, LastAssessedDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), TenantId = 1 });
            }

            return skills;
        }
    }
    #endregion
}
