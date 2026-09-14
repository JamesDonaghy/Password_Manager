namespace PasswordManager
{
    public class Account
    {
        public string Service { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Url { get; set; }
        public string Notes { get; set; }

        // Nullable because entries saved before these fields existed have no value here -
        // that's shown as "-" in the grid rather than a misleading default date.
        public System.DateTime? CreatedAt { get; set; }
        public System.DateTime? ModifiedAt { get; set; }

        // Whether the entry is marked as a favourite. Missing on older vaults; defaults to false.
        public bool IsFavorite { get; set; }
    }
}