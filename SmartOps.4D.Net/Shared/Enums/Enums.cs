using System;
using System.Collections.Generic;
using System.Text;

namespace SmartOps.Shared.Enums
{
    public enum SubscriptionPlan
    {
        Trial = 0,
        Starter = 1,
        Professional = 2,
        Business = 3
    }
    public enum ContractStatus
    {
        Reported = 0,
        SurveyBooked = 10,
        SurveyCompleted = 20,

        EstimateCompleted = 40,
        Authorised = 60,

        FittingBooked = 70,
        FittingCompleted = 80,

        Invoiced = 90,

    }

    public enum enum_item_type
    {
        none,
        assembly,
        panel,
        conservatory,
        garage,
        locking,
        glass,
        composite,
        bifold = 9,
        house,
        custom = 11
    }

    public enum enum_material_type
    {
        None = 0,
        UPVC = 1,
        Aluminium = 2,
        Timber = 4
    }

    public enum enum_special_glass_type
    {
        None = 0,
        BackToBack = 1,
        DiamondLead = 2,
        GeorgainBar = 3,
        GeorgianLead = 4
    }

    public enum enum_job_type
    {
        Survey,
        Fitting,
        Remedial,
        Complaint,
        Securing
    }

    public enum enum_tlight
    {
        Red,
        Orange,
        Green,
    }

    public enum agent_type
    {
        Surveyor,
        Fitter,
        Satellite
    }

}
