﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

using PagedList;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Threading.Tasks;

using ContosoUniversityFull.DAL;
using ContosoUniversityFull.Models;
using ContosoUniversityFull.Models.SchoolViewModels;
using ContosoUniversityFull.Services;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.Primitives;

namespace ContosoUniversityFull.Controllers
{
    public class StudentsController : Controller
    {
        private readonly SchoolContext db;

        private readonly IQueueSendClient<PictureJob> queueClient;

        public StudentsController(SchoolContext db, IQueueSendClient<PictureJob> queueClient)
        {
            this.db = db;
            this.queueClient = queueClient;
        }

        // GET: Student
        public ViewResult Index(string sortOrder, string currentFilter, string searchString, int? page)
        {
            ViewBag.CurrentSort = sortOrder;
            ViewBag.NameSortParm = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewBag.DateSortParm = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewBag.CurrentFilter = searchString;

            var students = from s in db.Students
                           select s;
            if (!String.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.LastName.Contains(searchString)
                                       || s.FirstMidName.Contains(searchString));
            }
            switch (sortOrder)
            {
                case "name_desc":
                    students = students.OrderByDescending(s => s.LastName);
                    break;
                case "Date":
                    students = students.OrderBy(s => s.EnrollmentDate);
                    break;
                case "date_desc":
                    students = students.OrderByDescending(s => s.EnrollmentDate);
                    break;
                default:  // Name ascending 
                    students = students.OrderBy(s => s.LastName);
                    break;
            }

            int pageSize = 100;
            int pageNumber = (page ?? 1);
            return View(students.ToPagedList(pageNumber, pageSize));
        }

        // GET: Student/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }

            ViewBag.StudentId = id;
            return View(student);
        }

        // GET: Student/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Student/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "LastName, FirstMidName, EnrollmentDate")]Student student, HttpPostedFileBase picture)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var userPicture = HandleUserPictureUpload(picture);

                    if (userPicture != null)
                    {
                        student.UserPicture = userPicture;
                    }

                    db.Students.Add(student);
                    db.SaveChanges();

                    if (student.UserPicture != null)
                    {
                        await EnqueuePictureJobAsync(student.UserPicture.PictureID);
                    }

                    return RedirectToAction("Index");
                }
            }
            catch (RetryLimitExceededException /* dex */)
            {
                //Log the error (uncomment dex variable name and add a line here to write a log.
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }
            return View(student);
        }

        public async Task EnqueuePictureJobAsync(int pictureId)
        {
            var load = new PictureJob()
            {
                PictureId = pictureId,
            };

            await queueClient.EnqueueMessageAsync(load);
        }

        private Picture HandleUserPictureUpload(HttpPostedFileBase picture)
        {
            if (picture != null && picture.ContentLength > 0)
            {
                var userPicture = new Picture()
                {
                    ContentType = picture.ContentType
                };

                using (var reader = new BinaryReader(picture.InputStream))
                {
                    userPicture.OriginalData = reader.ReadBytes(picture.ContentLength);
                }

                return userPicture;
            }
            return null;
        }

        // GET: Student/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        // POST: Student/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditPost(int? id, HttpPostedFileBase picture)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var studentToUpdate = db.Students.Find(id);
            if (TryUpdateModel(studentToUpdate, "",
               new string[] { "LastName", "FirstMidName", "EnrollmentDate" }))
            {
                var userPicture = HandleUserPictureUpload(picture);

                if (userPicture != null)
                {
                    studentToUpdate.UserPicture = userPicture;
                }

                try
                {
                    db.SaveChanges();

                    if (studentToUpdate.UserPicture != null)
                    {
                        await EnqueuePictureJobAsync(studentToUpdate.UserPicture.PictureID);
                    }

                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (RetryLimitExceededException /* dex */)
                {
                    //Log the error (uncomment dex variable name and add a line here to write a log.
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                }
            }
            return View(studentToUpdate);
        }

        // GET: Student/Delete/5
        public ActionResult Delete(int? id, bool? saveChangesError = false)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            if (saveChangesError.GetValueOrDefault())
            {
                ViewBag.ErrorMessage = "Delete failed. Try again, and if the problem persists see your system administrator.";
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        // POST: Student/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                Student student = db.Students.Find(id);
                db.Students.Remove(student);
                db.SaveChanges();
            }
            catch (RetryLimitExceededException/* dex */)
            {
                //Log the error (uncomment dex variable name and add a line here to write a log.
                return RedirectToAction("Delete", new { id = id, saveChangesError = true });
            }
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> Dashboard(int id)
        {
            StudentDashboardViewModel model = new StudentDashboardViewModel();

            model.Student = await db.Students
                                .Include(s => s.Enrollments)
                                .Include(s => s.Enrollments.Select(e => e.Course))
                                .AsNoTracking()
                                .SingleOrDefaultAsync(m => m.ID == id);

            ViewBag.StudentId = id;

            model.SuggestedCourses = db.Courses
                .Where(c => c.Enrollments.All(e => e.StudentID != id))
                .OrderBy(c => c.Enrollments.Count)
                .Take(10)
                .AsNoTracking().ToList();

            var departmentIds = db.Enrollments
                .Where(e => e.StudentID == id)
                .Select(e => e.Course.DepartmentID).Distinct();

            model.StudentDepartments = db.Departments
                .Where(d => departmentIds.Contains(d.DepartmentID))
                .AsNoTracking().ToList();

            if (model.Student == null)
            {
                return new HttpNotFoundResult();
            }

            return View(model);
        }

        [ActionName("UserPicture")]
        public async Task<FileResult> GetUserPicture(int? id, string type = "default")
        {
            if (id == null || string.IsNullOrWhiteSpace(type))
            {
                return File("/Content/UserImage.png", "image/png");
            }

            byte[] pictureData = null;

            IQueryable<Picture> pictureQuery = db.Pictures.Where(p => p.PictureID == id);

            switch (type)
            {
                case "thumbnail":
                    pictureData = await pictureQuery.Select(p => p.ThumbnailData).FirstOrDefaultAsync();
                    break;
                default:
                    pictureData = await pictureQuery.Select(p => p.Data).FirstOrDefaultAsync();
                    break;
            }

            if (pictureData == null || pictureData.Length == 0)
            {
                return File("/Content/UserImage.png", "image/png");
            }

            return File(pictureData, "image/jpg");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
