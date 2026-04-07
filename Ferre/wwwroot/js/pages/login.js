document.addEventListener('DOMContentLoaded', () => {
    const toggleButtons = document.querySelectorAll('.toggle-password');

    toggleButtons.forEach(button => {
        button.addEventListener('click', () => {
            const targetId = button.getAttribute('data-target');
            if (!targetId) {
                return;
            }

            const input = document.getElementById(targetId);
            if (!input) {
                return;
            }

            const isPassword = input.type === 'password';
            input.type = isPassword ? 'text' : 'password';

            const openIcon = button.querySelector('.eye-open');
            const closedIcon = button.querySelector('.eye-closed');
            openIcon?.classList.toggle('is-hidden', isPassword);
            closedIcon?.classList.toggle('is-hidden', !isPassword);

            button.setAttribute('aria-label', isPassword ? 'Ocultar contraseña' : 'Mostrar contraseña');
        });
    });
});
