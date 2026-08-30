using System.Collections.Generic;

namespace PasswordManager
{
    /// <summary>
    /// Tracks which accounts currently have their password shown (as opposed to masked)
    /// in the grid. Reference equality (the default for a class with no overridden Equals)
    /// is exactly what we want here - each Account is a distinct object, so this only
    /// tracks the specific entries the user has chosen to reveal, not accounts with
    /// equal-looking data.
    /// </summary>
    public class RevealedPasswordTracker
    {
        private readonly HashSet<Account> revealed = new HashSet<Account>();

        public bool IsRevealed(Account account)
        {
            return revealed.Contains(account);
        }

        /// Shows the password if it's currently hidden, or hides it again if already shown.
        public void Toggle(Account account)
        {
            if (!revealed.Remove(account))
            {
                revealed.Add(account);
            }
        }

        /// Stops tracking an account entirely - call this when the account itself is
        /// deleted, so this doesn't hold onto a stale reference.
        public void Forget(Account account)
        {
            revealed.Remove(account);
        }
    }
}