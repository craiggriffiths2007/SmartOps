public class JobBoardItemViewModel
{
    public int Id { get; set; }

    public string ClaimReference { get; set; } = "";

    public string Postcode { get; set; } = "";

    public DateTime IncidentDate { get; set; }

    public string CauseOfDamage { get; set; } = "";

    public string DamageDescription { get; set; } = "";

    public bool Requested { get; set; }
}