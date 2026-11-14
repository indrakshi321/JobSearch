# 🚀 SaaS Job Tracker & Career Discovery Platform

A high-end, full-stack SaaS job application tracking and discovery platform designed for software engineers. Built with modern enterprise architecture, local PDF resume parsing, experience-aware job filtering, and multi-source live job aggregation.

---

## 🛠️ Tech Stack

### Backend (`JobTracker.Api`)
* **Framework:** ASP.NET Core Web API (.NET 8)
* **ORM & Database:** Entity Framework Core with SQL Server (`JobApplication` entity persistence)
* **PDF Processing:** `PdfPig` for local resume text extraction and experience calculation
* **Architecture:** Repository and Service-oriented pattern with strict controller separation

### Frontend (`job-tracker-ui`)
* **Framework:** Angular (Standalone Components)
* **UI Library:** PrimeNG components with a custom dark-slate SaaS design theme
* **Layout Features:** Dual-tab view switching (Swipe Discovery deck and Saved Jobs folder table) with INR salary formatting and direct portal apply links

---

## ✨ Key Features

1. **Local PDF Resume Parsing:** Upload your resume directly to automatically extract technical skill sets and compute professional years of experience.
2. **Experience-Aware Job Discovery:** Dynamically filters out senior-level requirements, aligning opportunities for entry-level and junior positions.
3. **Multi-Source Job Aggregation:** Combines live openings from Remotive, Arbeitnow, and curated product/startup career portals.
4. **Interactive Management:** Track application statuses, save target listings to your personalized folder, and jump straight to direct company application portals.

---

## ⚙️ Getting Started Locally

### Prerequisites
* .NET 8 SDK
* Node.js & Angular CLI
* SQL Server (LocalDB or standard instance)

### Running the Backend
```bash
cd JobTracker.Api/JobTracker.Api
dotnet restore
dotnet run
