using Microsoft.CodeAnalysis.Elfie.Model.Tree;

namespace SmartOps.Web.Models
{

    public enum enum_vehicle_type
    {
        delivery_hgv, delivery_van, fitter_van, sales_car
    }

    public class VehicleCheckVehicle
    {
        public int Id { get; set; }
        public enum_vehicle_type VehicleType { get; set; }
        public string? VehicleName { get; set; }
        public string Registration { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Mileage { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string CheckerName { get; set; } = "";
        public bool? IsComplete { get; set; }
        public string ReasonNotCompleted { get; set; } = "";
        public string? ControllerName { get; set; }
        public string NameFromEnumType()
        {
            return NameFromEnumType(VehicleType);
        }
        public string NameFromEnumType(enum_vehicle_type type)
        {
            switch (type)
            {
                case enum_vehicle_type.delivery_hgv: return "Delivery HGV";
                case enum_vehicle_type.delivery_van: return "Delivery Van";
                case enum_vehicle_type.fitter_van: return "Fitter Van";
                case enum_vehicle_type.sales_car: return "Sales Car";
            }
            return "";
        }
        public string ControllerNameFromEnumType(enum_vehicle_type type)
        {
            switch (type)
            {
                case enum_vehicle_type.delivery_hgv: return "DeliveryHGVs";
                case enum_vehicle_type.delivery_van: return "DeliveryVans";
                case enum_vehicle_type.fitter_van: return "FitterVans";
                case enum_vehicle_type.sales_car: return "SalesCars";
            }
            return "";
        }

        public VehicleCheckVehicle(int id, enum_vehicle_type type, string reg)
        {
            Id = id;
            VehicleType = type;
            VehicleName = NameFromEnumType(type);
            ControllerName = ControllerNameFromEnumType(type);
            Registration = reg;
        }
    }

    public class SurveyItem
    {
        public int Id { get; set; }

        public bool bRepar { get; set; }
        public enum_material_type material { get; }
        public enum_item_type ItemType { get; set; }
        public enum_item_type BaseItemType { get; set; }
        public string? ItemTitle { get; set; }
        public int item_number { get; set; }
        public string? ControllerName { get; set; }
        public string ContractCode { get; set; } = "";

        public string ControllerNameFromEnumType(enum_item_type type) => "CustomItems";

        public SurveyItem(int id, enum_item_type type, enum_item_type base_type, int itemNumber, string contractCode, string _itemName)
        {
            Id = id;
            ItemType = type;
            BaseItemType = base_type;
            ItemTitle = _itemName;
            ControllerName = ControllerNameFromEnumType(type);
            item_number = itemNumber;
            ContractCode = contractCode;
        }
    }
}
