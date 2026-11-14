export interface Job {
  id?: string;
  company: string;
  role: string;
  location?: string;
  source?: string;
  dateApplied: string;
  status: number;
  notes?: string;
}

export interface DiscoveredJob {
  id: string;
  title: string;
  company: string;
  location: string;
  descriptionSnippet: string;
  salaryRange: string;
  experienceTag?: string;
  sourceBoard?: string;
  matchedSkills: string[];
  matchPercentage: number;
  directApplyUrl: string;
}

export interface ResumeUploadResponse {
  skills: string[];
  suggestedTitles: string[];
  yearsOfExperience?: number;
  experienceLevel?: string;
  feed: DiscoveredJob[];
}