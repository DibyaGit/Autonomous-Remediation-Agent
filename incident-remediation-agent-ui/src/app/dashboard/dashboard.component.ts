import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiagnosticService } from '../services/diagnostic.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent {
  errorInput: string = '';
  isLoading: boolean = false;
  isExecuting: boolean = false;
  result: string = '';
  executionStatus: string = '';

  private diagnosticService = inject(DiagnosticService);

  runDiagnostic(): void {
    if (!this.errorInput || this.errorInput.trim() === '') {
      this.result = 'Please enter a valid exception type.';
      return;
    }

    this.isLoading = true;
    this.result = '';
    this.executionStatus = '';

    this.diagnosticService.diagnoseError(this.errorInput).subscribe({
      next: (res) => {
        this.result = res;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.result = `Connection Error: Unable to communicate with the incident remediation server. Details: ${err.message || err.statusText || 'Server unreachable'}`;
      }
    });
  }

  approveAndExecuteFix(): void {
    if (!this.result || this.result.trim() === '') {
      return;
    }

    this.isExecuting = true;
    this.executionStatus = '';

    this.diagnosticService.executeFix(this.result).subscribe({
      next: (res) => {
        this.isExecuting = false;
        this.executionStatus = `SUCCESS: ${res}`;
      },
      error: (err) => {
        this.isExecuting = false;
        this.executionStatus = `EXECUTION FAILED: ${err.error || err.message || 'Error executing script'}`;
      }
    });
  }

  rejectScript(): void {
    this.result = '';
    this.executionStatus = 'Script rejected by human reviewer. No database changes were applied.';
  }
}
