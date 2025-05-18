import { configureStore } from '@reduxjs/toolkit';

import { shazamCoreApi } from './services/shazamCore';
import { audioArchiveApi } from './services/audioArchiveApi';
import playerReducer from './features/playerSlice';

export const store = configureStore({
  reducer: {
    [shazamCoreApi.reducerPath]: shazamCoreApi.reducer,
    [audioArchiveApi.reducerPath]: audioArchiveApi.reducer,
    player: playerReducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware()
    .concat(shazamCoreApi.middleware)
    .concat(audioArchiveApi.middleware),
}); 