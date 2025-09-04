namespace backend.DbModel
{
    public class User
    {
        // Primary key
        public int Id { get; set; }

        // Company account ID
        public string Gid { get; set; } = string.Empty;

        // Full display name
        public string FullName { get; set; } = string.Empty;

        // Email address
        public string Mail { get; set; } = string.Empty;

        // Role (e.g. Administrator, Worker)
        public string Role { get; set; } = string.Empty;
    }
}
