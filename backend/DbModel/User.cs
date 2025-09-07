namespace backend.DbModel
{
    public class User
    {
        // Primary key
        public int Id { get; set; }

        // Company account ID
        public string? Gid { get; set; }

        // Full display name
        public string? FullName { get; set; }

        // Email address
        public string? Mail { get; set; }

        // Role (e.g. Administrator, Worker)
        public string? Role { get; set; }
    }
}
