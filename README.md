# 🧭 Job Tracker (Angular + .NET 8 + SQLite/PostgreSQL)

A small full-stack app to track job applications.  
Frontend: **Angular + PrimeNG + Formly**  
Backend: **.NET 8 Web API + EF Core**  
DB: **SQLite (default)**, **PostgreSQL (optional)**

---

## ✅ Prerequisites

- **.NET 8 SDK** → https://dotnet.microsoft.com/download  
- **Node.js 18+** (includes npm) → https://nodejs.org  
- **Angular CLI**: `npm i -g @angular/cli`  
- **PostgreSQL 12+** *(optional, if switching from SQLite)*

> 💡 On Windows, the first HTTPS run may prompt you to trust the dev certificate.

---

## 🚀 Backend (API) — SQLite (default)

```bash
cd JobTracker.Api

# restore packages
dotnet restore

# (first time only) install EF CLI tools if you don't have them
dotnet tool install -g dotnet-ef

# create SQLite DB and schema
dotnet ef database update

# trust the dev HTTPS certificate (first time)
dotnet dev-certs https --trust

# run with hot reload
dotnet watch run

## 🚀 Frontend (UI) — Angular + PrimeNG

```bash
cd job-tracker-ui

# install dependencies
npm install

# start the dev server
npm start



## proxy.conf.json

{
  "/api": {
    "target": "http://localhost:5280",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
