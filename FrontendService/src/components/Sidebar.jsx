import React, { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { 
  HiOutlineHome, 
  HiOutlineMenu, 
  HiOutlineChartBar,
  HiOutlineUpload,
  HiOutlineLogin,
  HiOutlineLogout,
  HiOutlineUser
} from 'react-icons/hi';
import { RiCloseLine } from 'react-icons/ri';

import { logo } from '../assets';
import { useAuth } from './Auth/AuthProvider';

const links = [
  { name: 'Военный архив', to: '/', icon: HiOutlineHome },
  { name: 'Аналитика', to: '/analytics', icon: HiOutlineChartBar },
  { name: 'Загрузить запись', to: '/upload', icon: HiOutlineUpload },
];

const NavLinks = ({ handleClick }) => {
  const { isAuthenticated, user } = useAuth();
  
  return (
    <div className="mt-10">
      {links.map((item, index) => {
        return (
          <React.Fragment key={item.name}>
            {item.divider && <div className="my-8 h-[1px] bg-gray-600/30" />}
            <NavLink
              to={item.to}
              className="flex flex-row justify-start items-center my-8 text-sm font-medium text-gray-400 hover:text-cyan-400"
              onClick={() => handleClick && handleClick()}
            >
              <item.icon className="w-6 h-6 mr-2" />
              {item.name}
            </NavLink>
          </React.Fragment>
        );
      })}
    </div>
  );
};

const Sidebar = () => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const { loading } = useAuth();

  if (loading) {
    return (
      <div className="md:flex hidden flex-col w-[240px] py-10 px-4 bg-[#191624]">
        <img src={logo} alt="logo" className="w-full h-14 object-contain" />
        <p className="text-gray-400 text-sm mt-10">Загрузка...</p>
      </div>
    );
  }

  return (
    <>
      <div className="md:flex hidden flex-col w-[240px] py-10 px-4 bg-[#191624]">
        <img src={logo} alt="logo" className="w-full h-14 object-contain" />
        <NavLinks />
      </div>

      {/* Mobile sidebar */}
      <div className="absolute md:hidden block top-6 right-3">
        {!mobileMenuOpen ? (
          <HiOutlineMenu className="w-6 h-6 mr-2 text-white" onClick={() => setMobileMenuOpen(true)} />
        ) : (
          <RiCloseLine className="w-6 h-6 mr-2 text-white" onClick={() => setMobileMenuOpen(false)} />
        )}
      </div>

      <div className={`absolute top-0 h-screen w-2/3 bg-gradient-to-tl from-white/10 to-[#483D8B] backdrop-blur-lg z-10 p-6 md:hidden smooth-transition ${mobileMenuOpen ? 'left-0' : '-left-full'}`}>
        <img src={logo} alt="logo" className="w-full h-14 object-contain" />
        <NavLinks handleClick={() => setMobileMenuOpen(false)} />
      </div>
    </>
  );
};

export default Sidebar; 