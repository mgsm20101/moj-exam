import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { WaitingRoomComponent } from './waiting-room.component';
import { CandidateExamService } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

describe('WaitingRoomComponent', () => {
  let fixture: ComponentFixture<WaitingRoomComponent>;
  let component: WaitingRoomComponent;
  const router = { navigate: jasmine.createSpy('navigate') };
  const service = {
    queueStatus: jasmine.createSpy('queueStatus').and.returnValue(of({ status: 'Waiting', position: 2, estimatedWaitSeconds: 120 })),
    start: jasmine.createSpy('start')
  };
  const tokenStore = { set: () => {}, get: () => null, clear: () => {} };

  beforeEach(async () => {
    localStorage.setItem('queue_e1', JSON.stringify({ fullName: 'a b c d', nationalId: '29912310123404', mobileNumber: '01012345678' }));
    await TestBed.configureTestingModule({
      imports: [WaitingRoomComponent],
      providers: [
        { provide: CandidateExamService, useValue: service },
        { provide: AttemptTokenStore, useValue: tokenStore },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(WaitingRoomComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => { component.ngOnDestroy(); localStorage.removeItem('queue_e1'); });

  it('polls queue status and shows the position', () => {
    expect(service.queueStatus).toHaveBeenCalledWith('e1', '29912310123404');
    expect(component.position).toBe(2);
  });
});
