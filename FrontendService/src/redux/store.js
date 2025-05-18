import { configureStore } from '@reduxjs/toolkit';

import { audioArchiveApi } from './services/audioArchiveApi';
import playerReducer from './features/playerSlice';
import { authApi } from './services/authApi';

export const store = configureStore({
  reducer: {
    [audioArchiveApi.reducerPath]: audioArchiveApi.reducer,
    [authApi.reducerPath]: authApi.reducer,
    player: playerReducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware()
    .concat(audioArchiveApi.middleware, authApi.middleware)
}); 