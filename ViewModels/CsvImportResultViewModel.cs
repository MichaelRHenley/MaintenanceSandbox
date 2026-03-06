namespace MaintenanceSandbox.ViewModels
{
    public class CsvImportResultViewModel
    {
        public string ImportType { get; set; } = string.Empty;
        public bool IsDryRun { get; set; }

        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }

        public List<string> Errors { get; set; } = new();
    }
}

