import React from 'react';
import { useNavigate } from 'react-router-dom';

const ArtistCard = ({ author }) => {
  const navigate = useNavigate();

  return (
    <div
      className="flex flex-col w-[250px] p-4 bg-white/5 bg-opacity-80 backdrop-blur-sm animate-slideup rounded-lg cursor-pointer"
      onClick={() => navigate(`/authors/${author?.id}`)}
    >
      <img 
        alt="author_img" 
        src={author?.imageUrl || '/assets/default-artist.png'} 
        className="w-full h-56 rounded-lg object-cover" 
      />
      <p className="mt-4 font-semibold text-lg text-white truncate">
        {author?.name}
      </p>
      {author?.description && (
        <p className="text-sm text-gray-400 mt-1 truncate">
          {author.description}
        </p>
      )}
    </div>
  );
};

export default ArtistCard; 