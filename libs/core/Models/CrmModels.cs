using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

/// <summary>CRM Account — company in a sales context.</summary>
public class CrmAccount : INotifyPropertyChanged
{
    private int _id;
    private int? _companyId;
    private string _name = "";
    private string _accountType = "customer";
    private int? _accountOwnerId;
    private string _accountOwnerName = "";
    private decimal? _annualRevenue;
    private int? _employeeCount;
    private string _industry = "";
    private string _rating = "";
    private string _source = "";
    private DateTime? _lastActivityAt;
    private DateTime? _nextFollowUp;
    private string _stage = "prospecting";
    private string _website = "";
    private string _description = "";
    private string _tags = "";
    private bool _isActive = true;
    private DateTime? _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? CompanyId { get => _companyId; set { _companyId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string AccountType { get => _accountType; set { _accountType = value; N(); } }
    public int? AccountOwnerId { get => _accountOwnerId; set { _accountOwnerId = value; N(); } }
    public string AccountOwnerName { get => _accountOwnerName; set { _accountOwnerName = value; N(); } }
    public decimal? AnnualRevenue { get => _annualRevenue; set { _annualRevenue = value; N(); } }
    public int? EmployeeCount { get => _employeeCount; set { _employeeCount = value; N(); } }
    public string Industry { get => _industry; set { _industry = value; N(); } }
    public string Rating { get => _rating; set { _rating = value; N(); } }
    public string Source { get => _source; set { _source = value; N(); } }
    public DateTime? LastActivityAt { get => _lastActivityAt; set { _lastActivityAt = value; N(); } }
    public DateTime? NextFollowUp { get => _nextFollowUp; set { _nextFollowUp = value; N(); } }
    public string Stage { get => _stage; set { _stage = value; N(); } }
    public string Website { get => _website; set { _website = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string Tags { get => _tags; set { _tags = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public bool IsHot => Rating == "hot";
    public bool IsCustomer => AccountType == "customer";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Account ↔ Contact link with role.</summary>
public class AccountContactLink
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int ContactId { get; set; }
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string RoleInAccount { get; set; } = "user";
    public bool IsPrimary { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>Deal/Opportunity in the sales pipeline.</summary>
public class CrmDeal : INotifyPropertyChanged
{
    private int _id;
    private int? _accountId;
    private string _accountName = "";
    private int? _contactId;
    private string _contactName = "";
    private string _title = "";
    private string _description = "";
    private decimal? _value;
    private string _currency = "GBP";
    private int? _stageId;
    private string _stage = "";
    private int _probability = 50;
    private DateTime? _expectedClose;
    private DateTime? _actualClose;
    private int? _ownerId;
    private string _ownerName = "";
    private string _source = "";
    private string _competitor = "";
    private string _lossReason = "";
    private string _nextStep = "";
    private string _tags = "";
    private DateTime? _createdAt;
    private DateTime? _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? AccountId { get => _accountId; set { _accountId = value; N(); } }
    public string AccountName { get => _accountName; set { _accountName = value; N(); } }
    public int? ContactId { get => _contactId; set { _contactId = value; N(); } }
    public string ContactName { get => _contactName; set { _contactName = value; N(); } }
    public string Title { get => _title; set { _title = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public decimal? Value { get => _value; set { _value = value; N(); N(nameof(WeightedValue)); } }
    public string Currency { get => _currency; set { _currency = value; N(); } }
    public int? StageId { get => _stageId; set { _stageId = value; N(); } }
    public string Stage { get => _stage; set { _stage = value; N(); } }
    public int Probability { get => _probability; set { _probability = value; N(); N(nameof(WeightedValue)); } }
    public DateTime? ExpectedClose { get => _expectedClose; set { _expectedClose = value; N(); } }
    public DateTime? ActualClose { get => _actualClose; set { _actualClose = value; N(); } }
    public int? OwnerId { get => _ownerId; set { _ownerId = value; N(); } }
    public string OwnerName { get => _ownerName; set { _ownerName = value; N(); } }
    public string Source { get => _source; set { _source = value; N(); } }
    public string Competitor { get => _competitor; set { _competitor = value; N(); } }
    public string LossReason { get => _lossReason; set { _lossReason = value; N(); } }
    public string NextStep { get => _nextStep; set { _nextStep = value; N(); } }
    public string Tags { get => _tags; set { _tags = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime? UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public decimal WeightedValue => (Value ?? 0) * Probability / 100m;
    public bool IsClosedWon => Stage == "Closed Won";
    public bool IsClosedLost => Stage == "Closed Lost";
    public bool IsOpen => !IsClosedWon && !IsClosedLost;
    public int DaysToClose => ExpectedClose.HasValue
        ? (int)(ExpectedClose.Value - DateTime.UtcNow).TotalDays : 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Deal pipeline stage.</summary>
public class DealStage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; } = 100;
    public int Probability { get; set; } = 50;
    public bool IsWon { get; set; }
    public bool IsLost { get; set; }
    public string Color { get; set; } = "#808080";
    public bool IsActive { get; set; } = true;
}

/// <summary>Lead — unqualified contact.</summary>
public class CrmLead : INotifyPropertyChanged
{
    private int _id;
    private string _firstName = "";
    private string _lastName = "";
    private string _email = "";
    private string _phone = "";
    private string _companyName = "";
    private string _title = "";
    private string _source = "";
    private string _status = "new";
    private int _score;
    private int? _ownerId;
    private string _ownerName = "";
    private int? _convertedAccountId;
    private int? _convertedContactId;
    private int? _convertedDealId;
    private DateTime? _convertedAt;
    private string _notes = "";
    private string _tags = "";
    private DateTime? _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public string FirstName { get => _firstName; set { _firstName = value; N(); N(nameof(FullName)); } }
    public string LastName { get => _lastName; set { _lastName = value; N(); N(nameof(FullName)); } }
    public string Email { get => _email; set { _email = value; N(); } }
    public string Phone { get => _phone; set { _phone = value; N(); } }
    public string CompanyName { get => _companyName; set { _companyName = value; N(); } }
    public string Title { get => _title; set { _title = value; N(); } }
    public string Source { get => _source; set { _source = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public int Score { get => _score; set { _score = value; N(); N(nameof(Temperature)); } }
    public int? OwnerId { get => _ownerId; set { _ownerId = value; N(); } }
    public string OwnerName { get => _ownerName; set { _ownerName = value; N(); } }
    public int? ConvertedAccountId { get => _convertedAccountId; set { _convertedAccountId = value; N(); } }
    public int? ConvertedContactId { get => _convertedContactId; set { _convertedContactId = value; N(); } }
    public int? ConvertedDealId { get => _convertedDealId; set { _convertedDealId = value; N(); } }
    public DateTime? ConvertedAt { get => _convertedAt; set { _convertedAt = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }
    public string Tags { get => _tags; set { _tags = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsConverted => ConvertedAt.HasValue;
    public string Temperature => Score switch
    {
        >= 75 => "hot",
        >= 40 => "warm",
        _ => "cold"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>CRM activity (call, email, meeting, note, task).</summary>
public class CrmActivity
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";   // account, contact, deal, lead
    public int EntityId { get; set; }
    public string ActivityType { get; set; } = ""; // call, email, meeting, note, task
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Direction { get; set; } = "";    // inbound, outbound
    public int? DurationMinutes { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsCompleted { get; set; } = true;
    public int? LoggedBy { get; set; }
    public string LoggedByName { get; set; } = "";
    public int? RelatedTaskId { get; set; }
    public int? RelatedSdRequestId { get; set; }

    public bool IsOverdue => DueAt.HasValue && !IsCompleted && DueAt.Value < DateTime.UtcNow;
}

/// <summary>Product catalog entry.</summary>
public class CrmProduct : INotifyPropertyChanged
{
    private int _id;
    private string _sku = "";
    private string _name = "";
    private string _description = "";
    private string _category = "";
    private decimal _unitPrice;
    private string _currency = "GBP";
    private bool _isRecurring;
    private string _billingPeriod = "";
    private decimal _taxRatePct;
    private decimal? _costPrice;
    private bool _isActive = true;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Sku { get => _sku; set { _sku = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string Category { get => _category; set { _category = value; N(); } }
    public decimal UnitPrice { get => _unitPrice; set { _unitPrice = value; N(); N(nameof(MarginPct)); } }
    public string Currency { get => _currency; set { _currency = value; N(); } }
    public bool IsRecurring { get => _isRecurring; set { _isRecurring = value; N(); } }
    public string BillingPeriod { get => _billingPeriod; set { _billingPeriod = value; N(); } }
    public decimal TaxRatePct { get => _taxRatePct; set { _taxRatePct = value; N(); } }
    public decimal? CostPrice { get => _costPrice; set { _costPrice = value; N(); N(nameof(MarginPct)); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }

    public decimal? MarginPct => CostPrice.HasValue && UnitPrice > 0
        ? (UnitPrice - CostPrice.Value) / UnitPrice * 100m
        : null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Quote / proposal.</summary>
public class CrmQuote
{
    public int Id { get; set; }
    public int? DealId { get; set; }
    public int? AccountId { get; set; }
    public int? ContactId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public string Currency { get; set; } = "GBP";
    public decimal Subtotal { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPct { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string Notes { get; set; } = "";
    public string PdfUrl { get; set; } = "";

    public bool IsAccepted => Status == "accepted";
    public bool IsExpired => ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow && !IsAccepted;
}

public class CrmQuoteLine
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public int? ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxPct { get; set; }
    public int SortOrder { get; set; }
}
