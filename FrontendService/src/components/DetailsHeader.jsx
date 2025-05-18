import React from 'react';
import { Link } from 'react-router-dom';

const DetailsHeader = ({ artistId, artistData, songData }) => (
  <div className="relative w-full flex flex-col">
    <div className="w-full bg-gradient-to-l from-transparent to-black sm:h-48 h-28" />

    <div className="absolute inset-0 flex items-center">
      <img
        alt="profile"
        src={
          artistId 
            ? (artistData?.attributes?.artwork?.url?.replace('{w}', '500').replace('{h}', '500') || 'https://via.placeholder.com/400?text=Автор') 
            : (songData?.images?.coverart || songData?.coverImage || 'https://via.placeholder.com/400?text=Военная+запись')
        }
        className="sm:w-48 w-28 sm:h-48 h-28 rounded-full object-cover border-2 shadow-xl shadow-black"
      />

      <div className="ml-5">
        <p className="font-bold sm:text-3xl text-xl text-white">
          {artistId ? (artistData?.attributes?.name || artistData?.name) : songData?.title}
        </p>
        {!artistId && (
          <Link to={`/authors/${songData?.authorId || songData?.artists?.[0]?.adamid}`}>
            <p className="text-base text-gray-400 mt-2">{songData?.author || songData?.subtitle}</p>
          </Link>
        )}

        <p className="text-base text-gray-400 mt-2">
          {artistId
            ? artistData?.attributes?.genreNames?.[0]
            : songData?.year || songData?.genres?.primary}
        </p>
      </div>
    </div>

    <div className="w-full sm:h-44 h-24" />
  </div>
);

export default DetailsHeader; 