using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NavesPortalforWebWithCoreMvc.Controllers.AuthFromIntranetController;
using NavesPortalforWebWithCoreMvc.Data;
using NavesPortalforWebWithCoreMvc.RfSystemData;
using NuGet.Protocol.Core.Types;
using NavesPortalforWebWithCoreMvc.Models;
using NavesPortalforWebWithCoreMvc.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using NavesPortalforWebWithCoreMvc.Common;
using NavesPortalforWebWithCoreMvc.RfSystemModels;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Syncfusion.EJ2.Linq;
using Syncfusion.EJ2.Base;
using System.Collections;

namespace NavesPortalforWebWithCoreMvc.Controllers.DIS
{
    [Authorize]
    [CheckSession]
    public class DisItpMasterController : Controller
    {
        private readonly BM_NAVES_PortalContext _repository;
        private readonly IBM_NAVES_PortalContextProcedures _procedures;

        public DisItpMasterController(BM_NAVES_PortalContext repository, IBM_NAVES_PortalContextProcedures procedures)
        {
            _repository = repository;
            _procedures = procedures;
        }

        /// <summary>
        /// ITP Master Page
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Index()
        {
            return await Task.Run(() => View());
        }

        /// <summary>
        /// 목록
        /// </summary>
        /// <param name="SearchString"></param>
        /// <param name="StartDate"></param>
        /// <param name="EndDate"></param>
        /// <param name="dm"></param>
        /// <returns></returns>
        public async Task<IActionResult> UrlDataSource(string SearchString, DateTime? StartDate, DateTime? EndDate, [FromBody] DataManagerRequest? dm)
        {
            try
            {
                if (SearchString is null || SearchString == String.Empty)
                {
                    SearchString = "";
                }

                List<PNAV_DIS_GET_ITP_MASTER_LISTResult> resultList = await _procedures.PNAV_DIS_GET_ITP_MASTER_LISTAsync(SearchString.Trim(), StartDate, EndDate);

                IEnumerable DataSource = resultList.AsEnumerable();
                DataOperations operation = new DataOperations();

                //Search
                if (dm.Search != null && dm.Search.Count > 0)
                {
                    DataSource = operation.PerformSearching(DataSource, dm.Search);
                }

                if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
                {
                    DataSource = operation.PerformSorting(DataSource, dm.Sorted);
                }

                //Filtering
                if (dm.Where != null && dm.Where.Count > 0)
                {
                    DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
                }

                int count = DataSource.Cast<PNAV_DIS_GET_ITP_MASTER_LISTResult>().Count();

                //Paging
                if (dm.Skip != 0)
                {

                    DataSource = operation.PerformSkip(DataSource, dm.Skip);
                }

                if (dm.Take != 0)
                {
                    DataSource = operation.PerformTake(DataSource, dm.Take);
                }

                return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(new { result = DataSource });
            }
            catch (Exception e)
            {
                return RedirectToAction("SaveException", "Error", new { ex = e.InnerException.Message, returnController = "DisItpMaster", returnView = "Index" });
            }
        }

        public async Task<IActionResult> Detail(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tNAV_DIS_ITP_MASTER = await _repository.TNAV_DIS_ITP_MASTERs.FirstOrDefaultAsync(m => m.ITP_IDX == id);

            if (tNAV_DIS_ITP_MASTER == null)
            {
                return NotFound();
            }
            else
            {
                ViewBag.CreateUser = HttpContext.Session.GetString("UserName");
                ViewBag.part = _repository.VNAV_SELECT_DIS_CODE_PART_LISTs.ToList();
                ViewBag.group = _repository.VNAV_SELECT_DIS_CODE_GROUP_LISTs.ToList();
                ViewBag.Item = _repository.VNAV_SELECT_DIS_CODE_ITEM_LISTs.ToList();
                ViewBag.Inspection = _repository.VNAV_SELECT_DIS_CODE_INSPECTION_LISTs.ToList();

                // 첨부파일 목록
                ViewBag.FileList = await _repository.TNAV_COM_FILEs.Where(m => m.DOCUMENT_IDX == id && m.KIND_OF_FILES == "ITP_MASTER").ToListAsync();

                return View(tNAV_DIS_ITP_MASTER);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IActionResult Create()
        {
            ViewBag.CreateUser = HttpContext.Session.GetString("UserName");
            ViewBag.part = _repository.VNAV_SELECT_DIS_CODE_PART_LISTs.ToList();
            ViewBag.group = _repository.VNAV_SELECT_DIS_CODE_GROUP_LISTs.ToList();
            ViewBag.Item = _repository.VNAV_SELECT_DIS_CODE_ITEM_LISTs.ToList();
            ViewBag.Inspection = _repository.VNAV_SELECT_DIS_CODE_INSPECTION_LISTs.ToList();

            // Create new IDX for Project IDX
            ViewBag.CurrentIdx = Guid.NewGuid();
            return View();
        }

        /// <summary>
        /// Master ITP 생성
        /// </summary>
        /// <param name="tNAV_DIS_ITP_MASTER"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TNAV_DIS_ITP_MASTER tNAV_DIS_ITP_MASTER)
        {
            try
            {
                // 기존 Master ITP 여부
                if (tNAV_DIS_ITP_MASTER.IS_EXPANSION)
                {
                    // 확장 생성
                    // 해당 Inspection의 확장된 최대 번호 가져오기
                    TNAV_DIS_ITP_MASTER CurrentInspection = _repository.TNAV_DIS_ITP_MASTERs
                                    .Where (m => m.PART_CODE == tNAV_DIS_ITP_MASTER.PART_CODE && 
                                                 m.GROUP_CODE == tNAV_DIS_ITP_MASTER.GROUP_CODE && 
                                                 m.ITEM_CODE == tNAV_DIS_ITP_MASTER.ITEM_CODE)
                                    .OrderByDescending(m => m.INSPECTION_CODE)
                                    .AsNoTracking()
                                    .FirstOrDefault();
                    int CurrentInspectionCode = int.Parse(CurrentInspection.INSPECTION_CODE);


                    // Inspection Code 생성
                    tNAV_DIS_ITP_MASTER.INSPECTION_CODE = (CurrentInspectionCode + 1).ToString("00000");
                    tNAV_DIS_ITP_MASTER.EXPANSION = CurrentInspection.EXPANSION + 1;

                }
                else
                {
                    // 신규 생성
                    // 생성된 코드 중 확장된 Inspection을 제외한 최대값 가져오기
                    string Code = _repository.TNAV_DIS_ITP_MASTERs
                                    .Where(m => m.IS_EXPANSION == false)
                                    .OrderByDescending(m => m.INSPECTION_CODE)
                                    .Select(m => m.INSPECTION_CODE)
                                    .FirstOrDefault();

                    int CurrentInspectionCode = Convert.ToInt32(Code);

                    // Inspection Code 생성
                    tNAV_DIS_ITP_MASTER.INSPECTION_CODE = (CurrentInspectionCode + 10).ToString("00000");
                    tNAV_DIS_ITP_MASTER.EXPANSION = 0;
                }

                tNAV_DIS_ITP_MASTER.CODE = $"{tNAV_DIS_ITP_MASTER.PART_CODE}{tNAV_DIS_ITP_MASTER.GROUP_CODE}{tNAV_DIS_ITP_MASTER.ITEM_CODE}{tNAV_DIS_ITP_MASTER.INSPECTION_CODE}";
                tNAV_DIS_ITP_MASTER.PART = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "PART");
                tNAV_DIS_ITP_MASTER.GROUP = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "GROUP");
                tNAV_DIS_ITP_MASTER.ITEM = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "ITEM");

                tNAV_DIS_ITP_MASTER.CREATE_USER_NAME = HttpContext.Session.GetString("UserName");

                _repository.Add(tNAV_DIS_ITP_MASTER);
                await _repository.SaveChangesAsync();

                return RedirectToAction(nameof(Detail), new { id = tNAV_DIS_ITP_MASTER.ITP_IDX });
            }
            catch (Exception e)
            {
                return RedirectToAction("SaveException", "Error", new { ex = e.InnerException.Message, returnController = "DisItpMaster", returnView = "Create" });
            }
        }

        /// <summary>
        /// 수정 및 Expension 생성
        /// </summary>
        /// <param name="tNAV_DIS_ITP_MASTER"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCofirm(TNAV_DIS_ITP_MASTER tNAV_DIS_ITP_MASTER, string Expansion)
        {
            try
            {
                // 기존 Master ITP 여부 (on : 확장)
                if (Expansion == "on" || Expansion != null)
                {
                    TNAV_DIS_ITP_MASTER CurrentInspection = _repository.TNAV_DIS_ITP_MASTERs
                                    .Where(m => m.PART_CODE == tNAV_DIS_ITP_MASTER.PART_CODE &&
                                                 m.GROUP_CODE == tNAV_DIS_ITP_MASTER.GROUP_CODE &&
                                                 m.ITEM_CODE == tNAV_DIS_ITP_MASTER.ITEM_CODE)
                                    .OrderByDescending(m => m.INSPECTION_CODE)
                                    .AsNoTracking()
                                    .FirstOrDefault();
                    int CurrentInspectionCode = int.Parse(CurrentInspection.INSPECTION_CODE);

                    // Inspection Code 생성
                    tNAV_DIS_ITP_MASTER.INSPECTION_CODE = (CurrentInspectionCode + 1).ToString("00000");
                    tNAV_DIS_ITP_MASTER.EXPANSION = CurrentInspection.EXPANSION + 1;
                    tNAV_DIS_ITP_MASTER.IS_EXPANSION = true;

                    // 새로운 Master ITP 생성
                    tNAV_DIS_ITP_MASTER.ITP_IDX = Guid.NewGuid();
                    tNAV_DIS_ITP_MASTER.CREATE_USER_NAME = HttpContext.Session.GetString("UserName");

                    tNAV_DIS_ITP_MASTER.CODE = $"{tNAV_DIS_ITP_MASTER.PART_CODE}{tNAV_DIS_ITP_MASTER.GROUP_CODE}{tNAV_DIS_ITP_MASTER.ITEM_CODE}{tNAV_DIS_ITP_MASTER.INSPECTION_CODE}";
                    tNAV_DIS_ITP_MASTER.PART = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "PART");
                    tNAV_DIS_ITP_MASTER.GROUP = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "GROUP");
                    tNAV_DIS_ITP_MASTER.ITEM = getCodeName(tNAV_DIS_ITP_MASTER.PART_CODE, tNAV_DIS_ITP_MASTER.GROUP_CODE, tNAV_DIS_ITP_MASTER.ITEM_CODE, "ITEM");
                    _repository.Add(tNAV_DIS_ITP_MASTER);
                }
                else
                {
                    // 수정
                    tNAV_DIS_ITP_MASTER.MODIFY_USER_NAME = HttpContext.Session.GetString("UserName");
                    tNAV_DIS_ITP_MASTER.MODIFY_DATE = DateTime.Now;
                    tNAV_DIS_ITP_MASTER.IS_DELETED = false; // 삭제 기능 구현 시 값 수정되어야 함.

                    _repository.Update(tNAV_DIS_ITP_MASTER);
                }

                await _repository.SaveChangesAsync();

                return RedirectToAction(nameof(Detail), new { id = tNAV_DIS_ITP_MASTER.ITP_IDX });
            }
            catch (Exception e)
            {
                return RedirectToAction("SaveException", "Error", new { ex = e.InnerException.Message, returnController = "DisItpMaster", returnView = "Detail" });
            }
        }

        /// <summary>
        /// Master 코드 생성
        /// </summary>
        /// <param name="pART_CODE"></param>
        /// <param name="gROUP_CODE"></param>
        /// <param name="iTEM_CODE"></param>
        /// <param name="iNSPECTION_CODE"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private string getCreateCode(string pART_CODE, string gROUP_CODE, string iTEM_CODE, string iNSPECTION_CODE)
        {
            string result = string.Empty;

            return result;
        }

        /// <summary>
        /// 코드화된 내용
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string getCodeName(string _part_code, string _group_code, string _item_code, string type)
        {
            string result = string.Empty;
            switch (type)
            {
                case "PART":
                    result = _repository.TNAV_DIS_ITP_MASTERs.Where(m => m.PART_CODE == _part_code).Select(m => m.PART).First();
                    break;
                case "GROUP":
                    result = _repository.TNAV_DIS_ITP_MASTERs.Where(m => m.PART_CODE == _part_code && m.GROUP_CODE == _group_code).Select(m => m.GROUP).First();
                    break;
                case "ITEM":
                    result = _repository.TNAV_DIS_ITP_MASTERs.Where(m => m.PART_CODE == _part_code && m.GROUP_CODE == _group_code && m.ITEM_CODE == _item_code).Select(m => m.ITEM).First();
                    break;
            }
            return result;
        }
    }
}
