document.addEventListener('DOMContentLoaded', () => {
    let currentIndex = 0;
    const cards = document.querySelectorAll('.card');
    const cardWidth = 300;
    const maxIndex = cards.length - 3;

    function moveSlide(direction) {
        const carousel = document.getElementById('carousel');

        currentIndex += direction;

        if (currentIndex < 0) {
            currentIndex = 0;
        }

        if (currentIndex > maxIndex) {
            currentIndex = maxIndex;
        }

        carousel.style.transform = `translateX(${-currentIndex * cardWidth}px)`;
    }

    window.moveSlide = moveSlide;

    setInterval(() => {
        if (currentIndex < maxIndex) {
            moveSlide(1);
        } else {
            currentIndex = -1;
            moveSlide(1);
        }
    }, 5000);
});
