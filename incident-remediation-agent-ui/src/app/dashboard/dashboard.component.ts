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
  result: string = '';

  private diagnosticService = inject(DiagnosticService);

  runDiagnostic(): void {
    // Guard clause: input validation
    if (!this.errorInput || this.errorInput.trim() === '') {
      this.result = 'Please enter a valid exception type.';
      return;
    }

    this.isLoading = true;
    this.result = '';

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
}
