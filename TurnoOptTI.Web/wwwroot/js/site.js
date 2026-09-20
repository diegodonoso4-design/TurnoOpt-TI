// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// TurnoOpt-TI - Script nativo JavaScript 

document.addEventListener("DOMContentLoaded", () => {
    // 1. Confirmación de confirmación para aprobación de mallas
    const formAprobarMalla = document.querySelector("form[action*='Aprobar']");
    if (formAprobarMalla) {
        formAprobarMalla.addEventListener("submit", (e) => {
            const confirmed = confirm("¿Está seguro de aprobar la malla de turnos? Esta acción notificará a los operadores y fijará el calendario.");
            if (!confirmed) {
                e.preventDefault();
            }
        });
    }

    // 2. Validación de coherencia de fechas en formulario de ausencias
    const fechaInicio = document.getElementById("FechaInicio");
    const fechaFin = document.getElementById("FechaFin");

    if (fechaInicio && fechaFin) {
        const validarRangoFechas = () => {
            if (fechaInicio.value && fechaFin.value) {
                if (new Date(fechaFin.value) < new Date(fechaInicio.value)) {
                    fechaFin.setCustomValidity("La fecha de término no puede ser menor a la fecha de inicio.");
                } else {
                    fechaFin.setCustomValidity("");
                }
            }
        };

        fechaInicio.addEventListener("change", validarRangoFechas);
        fechaFin.addEventListener("change", validarRangoFechas);
    }

    // 3. Confirmación de asignación de reemplazos / horas extraordinarias
    const formsReemplazo = document.querySelectorAll("form[action*='ConfirmarReemplazo']");
    formsReemplazo.forEach(form => {
        form.addEventListener("submit", (e) => {
            const confirmed = confirm("¿Desea confirmar este reemplazo? Se imputarán las horas extras correspondientes en la base de datos.");
            if (!confirmed) {
                e.preventDefault();
            }
        });
    });
});