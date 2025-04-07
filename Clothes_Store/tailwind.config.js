module.exports = {
    prefix: 'tw-', // هتضيف بادئة لكل كلاسات tailwind
    corePlugins: {
        preflight: false, // دي عشان ميشتغلش الـ reset اللي بيخربط bootstrap
    },
    content: [
        "./Views/**/*.cshtml",
        "./wwwroot/js/**/*.js"
    ],
    theme: {
        extend: {},
    },
    plugins: [],
}