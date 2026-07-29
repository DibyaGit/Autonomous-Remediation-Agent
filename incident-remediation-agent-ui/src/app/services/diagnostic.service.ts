import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class DiagnosticService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  diagnoseError(errorLog: string): Observable<string> {
    return this.http.post(this.apiUrl, { errorLog }, { responseType: 'text' });
  }
}
