using Central.Core.Services;

namespace Central.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public void Configure_SetsIsConfigured()
    {
        var svc = new EmailService();
        Assert.False(svc.IsConfigured);

        svc.Configure("smtp.example.com", 587, "user", "pass", "noreply@example.com");
        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public void Configure_EmptyHost_NotConfigured()
    {
        var svc = new EmailService();
        svc.Configure("", 587, "user", "pass", "noreply@example.com");
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void Configure_EmptyFrom_NotConfigured()
    {
        var svc = new EmailService();
        svc.Configure("smtp.example.com", 587, "user", "pass", "");
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void Configure_FromDictionary()
    {
        var svc = new EmailService();
        svc.Configure(new Dictionary<string, string>
        {
            ["smtp_host"] = "smtp.test.com",
            ["smtp_port"] = "465",
            ["smtp_username"] = "user",
            ["smtp_password"] = "pass",
            ["smtp_from_address"] = "central@test.com",
            ["smtp_from_name"] = "Central",
            ["smtp_use_ssl"] = "true"
        });
        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public async Task SendAsync_NotConfigured_ReturnsFalse()
    {
        var svc = new EmailService();
        var result = await svc.SendAsync("test@example.com", "Test", "Body");
        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_Configured_NoSmtp_ReturnsFalse()
    {
        // Configured but no SMTP server running — will fail gracefully
        var svc = new EmailService();
        svc.Configure("localhost", 25, "", "", "test@test.com");
        var result = await svc.SendAsync("to@test.com", "Test", "Body");
        Assert.False(result); // Connection refused — handled gracefully
    }
}
