namespace Core.Constants
{
    // SupportConversation.Type discriminator — one table now serves two distinct
    // chat products (see SupportConversation entity remarks):
    //   AdminSupport — a guest's single conversation with the concierge/admin team.
    //   DriverGuest  — one conversation per (guest, driver) pair.
    public static class SupportChatTypes
    {
        public const string AdminSupport = "AdminSupport";
        public const string DriverGuest = "DriverGuest";
    }
}
