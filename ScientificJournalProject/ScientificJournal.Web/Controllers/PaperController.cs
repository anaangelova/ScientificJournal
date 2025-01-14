﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScientificJournal.Domain.DomainModels;
using ScientificJournal.Domain.DTO;
using ScientificJournal.Domain.Identity;
using ScientificJournal.Service.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ScientificJournal.Web.Controllers
{
    [Authorize]
    public class PaperController : Controller
    {

        private readonly IPaperService paperService;
        private readonly IPaperDocumentService paperDocumentService;
        private readonly IConferenceService conferenceService;
        private readonly UserManager<ScienceUser> userManager;

        public PaperController(IPaperService paperService, IPaperDocumentService paperDocumentService, IConferenceService conferenceService, UserManager<ScienceUser> userManager)
        {
            this.paperService = paperService;
            this.paperDocumentService = paperDocumentService;
            this.conferenceService = conferenceService;
            this.userManager = userManager;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            //gi lista site approved papers + site konferencii da se prikazhat
            List<Paper> allPapers = paperService.GetAllPapers();
            foreach (Paper p in allPapers)
            {
                if (p.Abstract.Length > 670)
                {
                    p.Abstract = p.Abstract.Substring(0, 670) + "...";
                }
            }
            List<Conference> allConferences = conferenceService.GetConferences();
            PapersConferencesDTO dto = new PapersConferencesDTO
            {
                Papers = allPapers,
                Conferences = allConferences
            };
            return View(dto);
        }

        public IActionResult Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;
            List<Conference> conferences = conferenceService.GetConferences();
            PaperDTO model = new PaperDTO
            {
                AuthorFirst = currentUser.Email,
                Conferences = conferences
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind] PaperDTO paperDTO, IFormFile file)
        {
            if (ModelState.IsValid)
            {
                
                paperService.CreateNewPaper(paperDTO,file);
                return RedirectToAction("MyPapers");


            }
            return View(paperDTO);
        }

        [AllowAnonymous]
        public IActionResult Details(Guid? id, int? flag, string? prev)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paper = paperService.GetDetailsForPaper(id);
            if (paper == null)
            {
                return NotFound();
            }
            if (flag == 1)
            {
                paper.NoEdit = true;
            } //inaku po default e false
            if(prev!=null && prev != "")
            {
                paper.PreviousAction = prev;
            }else paper.PreviousAction = "Index";

            return View(paper);
        }

        [AllowAnonymous]
        public IActionResult GetPdfDocument(Guid? documentId)
        {
            String name = paperDocumentService.GetPaper(documentId).DocumentName;
            string pathToUpload = $"{Directory.GetCurrentDirectory()}\\files\\{name}";
            var stream = new FileStream(pathToUpload, FileMode.Open);
            return File(stream, "application/pdf", name);
        }

        
        public IActionResult MyPapers()
        {
            //treba da gi izlista samo papers na najaveniot korisnik
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            List<Paper> papers = paperService.GetPapersForUser(userId);

            return View(papers);
        }


        public IActionResult Edit(Guid? id)
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;

            PaperDTO tmp= paperService.GetDetailsForEdit(id,currentUser);
            string authors = tmp.AuthorFirst + " " + tmp.AuthorSecond + " " + tmp.AuthorThird;


            if (!isAuthor(authors))
            {
                return StatusCode(403);
            }
            if (id == null)
            {
                return NotFound();
            }

            var paper = paperService.GetDetailsForEdit(id, currentUser);
            if (paper == null)
            {
                return NotFound();
            }
           
            return View(paper);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Guid id, [Bind("Paper,Keywords,ConferenceName,ConferenceId,AuthorFirst,AuthorSecond,AuthorThird")] PaperDTO paperDto)
        {
            string authors = paperDto.AuthorFirst + " " + paperDto.AuthorSecond + " " + paperDto.AuthorThird;
            if (!isAuthor(authors))
            {
                return StatusCode(403);
            }
            if (id != paperDto.Paper.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    paperService.UpdateExistingPaper(paperDto);
                    
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaperExists(paperDto.Paper.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction("MyPapers");
            }
            return View(paperDto);
        }
        public IActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paper = paperService.GetDetailsForPaper(id);
            if (paper == null)
            {
                return NotFound();
            }

            return View(paper);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(Guid id)
        {
            paperService.DeletePaper(id);
            
            return RedirectToAction("MyPapers");
        }

        private bool PaperExists(Guid? id)
        {
            return paperService.GetDetailsForPaper(id) != null;
        }

        public IActionResult ShowPendingPapers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;
            if (currentUser.isAdmin)
            {
                List<Paper> pendingPapers = paperService.GetAllPendingPapers();
                return View(pendingPapers);
            }
            else return View("AdminRoleRequiered");

        }
        

        public IActionResult ApprovePaper(Guid? id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;
            if (currentUser.isAdmin)
            {
                paperService.ApprovePaper(id);
                return RedirectToAction("ShowPendingPapers");
            }
            else return View("AdminRoleRequiered");
        }

        public IActionResult DenyPaper(Guid? id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;
            if (currentUser.isAdmin)
            {
                paperService.DenyPaper(id);
                return RedirectToAction("ShowPendingPapers");
            }
            else return View("AdminRoleRequiered");
        }

        private bool isAuthor(string authors)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ScienceUser currentUser = userManager.FindByIdAsync(userId.ToString()).Result;
            string authorEmail = currentUser.Email;
            return authors.Split(" ").Any(a => a.Equals(authorEmail));
            
        }

        
    }
}
