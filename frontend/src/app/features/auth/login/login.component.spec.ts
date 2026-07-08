import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService, LoginResponse } from '../../../core/services/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj('AuthService', ['login']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent, ReactiveFormsModule],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('navigates to /admin on successful login', () => {
    const response: LoginResponse = { token: 't', refreshToken: 'r', userName: 'admin', roles: ['Admin'] };
    authService.login.and.returnValue(of(response));
    component.form.setValue({ userName: 'admin', password: 'secret' });

    component.submit();

    expect(authService.login).toHaveBeenCalledWith('admin', 'secret');
    expect(router.navigate).toHaveBeenCalledWith(['/admin']);
    expect(component.errorMessage).toBeNull();
  });

  it('shows an error message on failed login', () => {
    authService.login.and.returnValue(throwError(() => ({ status: 401 })));
    component.form.setValue({ userName: 'admin', password: 'wrong' });

    component.submit();

    expect(component.errorMessage).toBe('اسم المستخدم أو كلمة المرور غير صحيحة.');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
