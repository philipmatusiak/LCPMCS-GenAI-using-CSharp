namespace CustomerManagementApp.Services
{
    public interface IEmailService
    {
        void SendWelcomeEmail(string email, string name);
    }
}
