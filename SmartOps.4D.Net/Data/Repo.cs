

using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using SQLitePCL;

namespace SmartOps.Web.Data
{
    public interface IRepo // no real need for this interface
    {
        AppDBContext Db { get; }

        IEnumerable<PhotoImage> GetSurveyItemImages(string contractCode, int itemNumber);
    }
    public class Repo : IRepo
    {
        private readonly AppDBContext _context;

        public AppDBContext Db => _context;

        public Repo(AppDBContext context)
        {
            _context = context;
        }

        public IEnumerable<PhotoImage> GetSurveyItemImages(string contractCode, int itemNumber)
        {
            string pattern = $"{contractCode:00000000}____{itemNumber:000}%"; // using _ as a wildcard ( would have been cAZ and dAZ )

            var photoimages = _context.Images
                .Where(x => EF.Functions.Like(x.Filename, pattern))
                .OrderByDescending(x => x.Filename.Substring(9, 1))
                .ToList();

            return photoimages ?? new List<PhotoImage>();
        }



        public VehicleCheckDetailsViewModel GetVehicleCheckDetailsViewModel(int id)
        {
            var viewModel = new VehicleCheckDetailsViewModel();
            viewModel.VehicleCheck = _context.VehicleChecks.FirstOrDefault(m => m.Id == id);
            if (viewModel.VehicleCheck == null) return viewModel;
            var vehicleType=(enum_vehicle_type)viewModel.VehicleCheck.VehicleType;
            string suffix=vehicleType switch { enum_vehicle_type.delivery_hgv=>"dhgv", enum_vehicle_type.delivery_van=>"dvan", enum_vehicle_type.fitter_van=>"fvan", enum_vehicle_type.sales_car=>"scar", _=>"" };
            string pattern=$"{viewModel.VehicleCheck.CheckID}_{viewModel.VehicleCheck.item_number:D8}_{suffix}%";
            var photoimages=_context.Images.Where(x=>EF.Functions.Like(x.Filename,pattern)).ToList();
            string Part(PhotoImage i,int start,int len)=>i.Filename.Length>=start+len?i.Filename.Substring(start,len):"";
            viewModel.photoLeft=photoimages.FirstOrDefault(i=>Part(i,43,3)=="pas")??new PhotoImage();
            viewModel.photoRight=photoimages.FirstOrDefault(i=>Part(i,43,3)=="drv")??new PhotoImage();
            viewModel.photoFront=photoimages.FirstOrDefault(i=>Part(i,43,3)=="fro")??new PhotoImage();
            viewModel.photoBack=photoimages.FirstOrDefault(i=>Part(i,43,3)=="rea")??new PhotoImage();
            viewModel.diagramLeft=photoimages.FirstOrDefault(i=>Part(i,43,3)=="pad")??new PhotoImage();
            viewModel.diagramRight=photoimages.FirstOrDefault(i=>Part(i,43,3)=="drd")??new PhotoImage();
            viewModel.diagramFront=photoimages.FirstOrDefault(i=>Part(i,43,3)=="frd")??new PhotoImage();
            viewModel.diagramBack=photoimages.FirstOrDefault(i=>Part(i,43,3)=="red")??new PhotoImage();
            viewModel.drv_signature=photoimages.FirstOrDefault(i=>Part(i,43,3)=="dsi")??new PhotoImage();
            viewModel.chk_signature=photoimages.FirstOrDefault(i=>Part(i,43,3)=="csi")??new PhotoImage();
            viewModel.insidePhotos=photoimages.Where(i=>Part(i,43,1)=="i").ToList();
            return viewModel;
        }
}
}
