import { HttpInterceptorFn } from '@angular/common/http';

export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const tenantReq = req.clone({
    headers: req.headers.set('X-Tenant-ID', 'client-wipro')
  });
  return next(tenantReq);
};
