import React, { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';

export default function PasswordInput(props) {
  const [show, setShow] = useState(false);
  const { className = '', ...rest } = props;

  return (
    <div className="relative">
      <input
        type={show ? 'text' : 'password'}
        className={`${className} pr-8`}
        {...rest}
      />
      <button
        type="button"
        onClick={() => setShow((prev) => !prev)}
        className="absolute right-2 top-1/2 -translate-y-1/2 text-text-3 hover:text-text transition-colors"
        tabIndex={-1}
      >
        {show ? <EyeOff size={14} /> : <Eye size={14} />}
      </button>
    </div>
  );
}
