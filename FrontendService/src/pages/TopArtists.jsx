import React from 'react';

import { ArtistCard, Error, Loader } from '../components';
import { useGetAuthorsQuery } from '../redux/services/audioArchiveApi';

const TopArtists = () => {
  const { data, isFetching, error } = useGetAuthorsQuery();

  if (isFetching) return <Loader title="Загрузка авторов..." />;

  if (error) return <Error />;

  return (
    <div className="flex flex-col">
      <h2 className="font-bold text-3xl text-white text-left mt-4 mb-10">Популярные авторы</h2>

      <div className="flex flex-wrap sm:justify-start justify-center gap-8">
        {data?.map((author) => <ArtistCard key={author.id} author={author} />)}
      </div>
    </div>
  );
};

export default TopArtists; 