namespace Abs.FixedAssets.Services;

public enum DeploymentMode
{
    SingleTenant = 0,
    MultiTenant = 1
}

public class TenantSettings
{
    public DeploymentMode DeploymentMode { get; set; } = DeploymentMode.SingleTenant;
    public int DefaultTenantId { get; set; } = 1;
    public int DefaultCompanyId { get; set; } = 1;
    public int DefaultSiteId { get; set; } = 1;
}

public interface ITenantContext
{
    int? TenantId { get; }
    int? CompanyId { get; }
    int? SiteId { get; }
    int? AssignedCompanyId { get; }
    int? AssignedSiteId { get; }
    List<int> VisibleCompanyIds { get; }
    List<int> VisibleSiteIds { get; }
    bool IsResolved { get; }
    string? ResolutionError { get; }
    
    void SetContext(int? tenantId, int? companyId, int? siteId);
    void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds);
    void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds);
    void SetError(string error);
}

public interface ITenantContextOverride
{
    IDisposable BeginScope(int tenantId, int companyId, int? siteId = null, int? userId = null);
    TenantContextOverrideValues? GetCurrentOverride();
}

public class TenantContextOverrideValues
{
    public int TenantId { get; set; }
    public int CompanyId { get; set; }
    public int? SiteId { get; set; }
    public int? UserId { get; set; }
}

public class TenantContextOverride : ITenantContextOverride
{
    private static readonly AsyncLocal<Stack<TenantContextOverrideValues>> _scopeStack = new();

    public IDisposable BeginScope(int tenantId, int companyId, int? siteId = null, int? userId = null)
    {
        var stack = _scopeStack.Value ??= new Stack<TenantContextOverrideValues>();
        var values = new TenantContextOverrideValues
        {
            TenantId = tenantId,
            CompanyId = companyId,
            SiteId = siteId,
            UserId = userId
        };
        stack.Push(values);
        return new TenantContextScope(stack);
    }

    public TenantContextOverrideValues? GetCurrentOverride()
    {
        var stack = _scopeStack.Value;
        if (stack != null && stack.Count > 0)
        {
            return stack.Peek();
        }
        return null;
    }

    private class TenantContextScope : IDisposable
    {
        private readonly Stack<TenantContextOverrideValues> _stack;
        private bool _disposed;

        public TenantContextScope(Stack<TenantContextOverrideValues> stack)
        {
            _stack = stack;
        }

        public void Dispose()
        {
            if (!_disposed && _stack.Count > 0)
            {
                _stack.Pop();
                _disposed = true;
            }
        }
    }
}

public class TenantContext : ITenantContext
{
    private readonly ITenantContextOverride? _override;
    
    private int? _tenantId;
    private int? _companyId;
    private int? _siteId;
    private int? _assignedCompanyId;
    private int? _assignedSiteId;
    private List<int> _visibleCompanyIds = new();
    private List<int> _visibleSiteIds = new();

    public TenantContext()
    {
    }

    public TenantContext(ITenantContextOverride tenantContextOverride)
    {
        _override = tenantContextOverride;
    }

    public int? TenantId
    {
        get
        {
            var overrideValues = _override?.GetCurrentOverride();
            if (overrideValues != null)
            {
                return overrideValues.TenantId;
            }
            return _tenantId;
        }
        private set => _tenantId = value;
    }

    public int? CompanyId
    {
        get
        {
            var overrideValues = _override?.GetCurrentOverride();
            if (overrideValues != null)
            {
                return overrideValues.CompanyId;
            }
            return _companyId;
        }
        private set => _companyId = value;
    }

    public int? SiteId
    {
        get
        {
            var overrideValues = _override?.GetCurrentOverride();
            if (overrideValues != null)
            {
                return overrideValues.SiteId;
            }
            return _siteId;
        }
        private set => _siteId = value;
    }

    public int? AssignedCompanyId => _assignedCompanyId;

    public int? AssignedSiteId => _assignedSiteId;

    public List<int> VisibleCompanyIds => _visibleCompanyIds;

    public List<int> VisibleSiteIds => _visibleSiteIds;

    public bool IsResolved
    {
        get
        {
            var overrideValues = _override?.GetCurrentOverride();
            if (overrideValues != null)
            {
                return true;
            }
            return TenantId.HasValue;
        }
    }

    public string? ResolutionError { get; private set; }

    public void SetContext(int? tenantId, int? companyId, int? siteId)
    {
        _tenantId = tenantId;
        _companyId = companyId;
        _siteId = siteId;
        ResolutionError = null;
    }

    public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds)
    {
        _assignedCompanyId = assignedCompanyId;
        _visibleCompanyIds = visibleCompanyIds;
    }

    public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds)
    {
        _assignedSiteId = assignedSiteId;
        _visibleSiteIds = visibleSiteIds;
    }

    public void SetError(string error)
    {
        ResolutionError = error;
    }
}
