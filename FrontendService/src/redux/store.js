import { configureStore } from '@reduxjs/toolkit';

import { audioArchiveApi } from './services/audioArchiveApi';
import playerReducer from './features/playerSlice';

export const store = configureStore({
  reducer: {
    [audioArchiveApi.reducerPath]: audioArchiveApi.reducer,
    player: playerReducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware()
    .concat(audioArchiveApi.middleware),
}); 