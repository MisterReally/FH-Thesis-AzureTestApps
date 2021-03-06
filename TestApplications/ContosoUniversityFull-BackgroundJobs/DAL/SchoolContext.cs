﻿using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.SqlClient;

using ContosoUniversityFull.Models;

namespace ContosoUniversityFull.DAL
{
    public class SchoolContext : DbContext
    {
        public SchoolContext() : base(GetConnectionString()) { }

        private static string GetConnectionString() =>
            new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["SchoolContext"].ConnectionString)
            {
                ConnectRetryCount = 5,
                ConnectRetryInterval = 2,
                MaxPoolSize = 900,
                MinPoolSize = 5
            }.ToString();

        public DbSet<Course> Courses { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<OfficeAssignment> OfficeAssignments { get; set; }
        public DbSet<Person> People { get; set; }
        public DbSet<Picture> Pictures { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<Course>()
                .HasMany(c => c.Instructors).WithMany(i => i.Courses)
                .Map(t => t.MapLeftKey("CourseID")
                    .MapRightKey("InstructorID")
                    .ToTable("CourseInstructor"));

            // removed because direct access is more suited for pattern tests 
            // modelBuilder.Entity<Department>().MapToStoredProcedures();
        }
    }
}