import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { CardModule } from 'primeng/card';
import { ProgressBarModule } from 'primeng/progressbar';
import { JobService } from './services/job.service';
import { Job, DiscoveredJob } from './models/job.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    TagModule,
    InputTextModule,
    DialogModule,
    CardModule,
    ProgressBarModule
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private jobService = inject(JobService);

  activeTab: 'tracker' | 'discover' = 'discover';

  jobs: Job[] = [];
  filteredJobs: Job[] = [];
  loading: boolean = true;
  searchQuery: string = '';

  // Resume & Discovery State
  isUploadingResume: boolean = false;
  detectedSkills: string[] = [];
  detectedExperience: string = '';
  suggestedTitles: string[] = [];
  jobFeed: DiscoveredJob[] = [];
  currentCardIndex: number = 0;
  lastSavedMessage: string = '';

  // Manual Modal
  displayAddModal: boolean = false;
  newJob: Job = this.createEmptyJob();

  ngOnInit() {
    this.loadJobs();
  }

  private getTodayDateOnly(): string {
    return new Date().toISOString().split('T')[0]; // Returns "YYYY-MM-DD"
  }

  private createEmptyJob(): Job {
    return {
      company: '',
      role: '',
      status: 0, // Applied
      location: '',
      source: 'Manual Entry',
      dateApplied: this.getTodayDateOnly(),
      notes: ''
    };
  }

  loadJobs() {
    this.loading = true;
    this.jobService.getJobs().subscribe({
      next: (data: any) => {
        this.jobs = Array.isArray(data) ? data : (data?.items || []);
        this.filterJobs();
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load applications', err);
        this.loading = false;
      }
    });
  }

  filterJobs() {
    if (!this.searchQuery.trim()) {
      this.filteredJobs = [...this.jobs];
      return;
    }
    const q = this.searchQuery.toLowerCase();
    this.filteredJobs = this.jobs.filter(j =>
      j.company?.toLowerCase().includes(q) ||
      j.role?.toLowerCase().includes(q) ||
      j.location?.toLowerCase().includes(q)
    );
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (!file) return;

    this.isUploadingResume = true;
    this.jobService.uploadResume(file).subscribe({
      next: (res) => {
        this.detectedSkills = res.skills;
        this.detectedExperience = res.experienceLevel || '';
        this.suggestedTitles = res.suggestedTitles;
        this.jobFeed = res.feed;
        this.currentCardIndex = 0;
        this.isUploadingResume = false;
        this.activeTab = 'discover';
      },
      error: (err) => {
        console.error('Upload failed', err);
        this.isUploadingResume = false;
        alert('Could not parse resume. Make sure it is a valid PDF.');
      }
    });
  }

  get currentJob(): DiscoveredJob | null {
    if (!this.jobFeed || this.jobFeed.length === 0) return null;
    if (this.currentCardIndex >= this.jobFeed.length) return null;
    return this.jobFeed[this.currentCardIndex];
  }

  passJob() {
    if (this.currentCardIndex < this.jobFeed.length) {
      this.currentCardIndex++;
    }
  }

  likeJob() {
    const job = this.currentJob;
    if (!job) return;
    

    const newEntry: Job = {
      company: (job.company || 'Unknown Company').slice(0, 120),
      role: (job.title || 'Software Developer').slice(0, 120),
      location: job.location || 'India / Remote',
      source: job.sourceBoard || 'CareerTrack Discovery',
      dateApplied: this.getTodayDateOnly(),
      status: 4, // AppStatus.OnHold (Saved / Interested)
      notes: job.directApplyUrl
    };

    this.currentCardIndex++;

    this.jobService.createJob(newEntry).subscribe({
      next: () => {
        this.loadJobs();
        this.lastSavedMessage = `Saved "${job.title}" at ${job.company} to your Saved Jobs folder!`;
        setTimeout(() => this.lastSavedMessage = '', 3000);
      },
      error: (err) => {
        console.error('Database save failed:', err);
        alert(`Could not save job. Status: ${err.status}`);
      }
    });
  }

  markAsApplied(job: Job) {
    if (!job.id) return;
    const updated: Job = {
      ...job,
      status: 0, // AppStatus.Applied
      dateApplied: this.getTodayDateOnly()
    };
    this.jobService.updateJob(job.id, updated).subscribe({
      next: () => this.loadJobs(),
      error: (err) => console.error('Error updating status', err)
    });
  }

  openAddModal() {
    this.newJob = this.createEmptyJob();
    this.displayAddModal = true;
  }

  saveNewJob() {
    if (!this.newJob.company || !this.newJob.role) return;
    this.newJob.status = Number(this.newJob.status);
    this.jobService.createJob(this.newJob).subscribe({
      next: () => {
        this.displayAddModal = false;
        this.loadJobs();
      },
      error: (err) => console.error('Error saving job', err)
    });
  }

  deleteJob(id?: string) {
    if (!id) return;
    if (confirm('Delete this saved application?')) {
      this.jobService.deleteJob(id).subscribe({
        next: () => this.loadJobs(),
        error: (err) => console.error('Error deleting job', err)
      });
    }
  }

  getCountByStatus(statusCode: number): number {
    if (!this.jobs || !Array.isArray(this.jobs)) return 0;
    return this.jobs.filter(j => j.status === statusCode).length;
  }

  getStatusLabel(statusCode: number): string {
    switch (statusCode) {
      case 0: return 'Applied';
      case 1: return 'Interview';
      case 2: return 'Offer';
      case 3: return 'Rejected';
      case 4: return 'Saved / Interested';
      default: return 'Saved';
    }
  }

  getSeverity(statusCode: number) {
    switch (statusCode) {
      case 0: return 'info';
      case 1: return 'warn';
      case 2: return 'success';
      case 3: return 'danger';
      case 4: return 'secondary';
      default: return 'contrast';
    }
  }
}