import { Routes } from '@angular/router';
import { ChatComponent } from './chat/chat.component';
import { CompareComponent } from './compare/compare.component';
import { RecommendComponent } from './recommend/recommend.component';

export const routes: Routes = [
  { path: '', redirectTo: 'chat', pathMatch: 'full' },
  { path: 'chat', component: ChatComponent },
  { path: 'compare', component: CompareComponent },
  { path: 'recommend', component: RecommendComponent },
  { path: '**', redirectTo: 'chat' }
];
