using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Services
{
    public class TurnoEngineService : ITurnoEngineService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRuleValidationService _ruleValidator;

        public TurnoEngineService(ApplicationDbContext context, IRuleValidationService ruleValidator)
        {
            _context = context;
            _ruleValidator = ruleValidator;
        }

        public async Task<MallaCabecera> GenerarMallaAsync(int idEsquema, int mes, int anio, int idSupervisorCrea)
        {
            var esquema = await _context.EsquemasCobertura.FindAsync(idEsquema);
            if (esquema == null)
                throw new InvalidOperationException("El esquema de cobertura seleccionado no existe.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Limpieza transaccional de mallas borrador previas en el período
                var mallaExistente = await _context.MallasCabecera
                    .Include(m => m.Turnos)
                    .FirstOrDefaultAsync(m => m.PeriodoMes == mes && m.PeriodoAnio == anio);

                if (mallaExistente != null)
                {
                    if (mallaExistente.EstadoMalla == "Aprobada")
                        throw new InvalidOperationException($"Ya existe una malla oficial APROBADA para el período {mes}/{anio}. No puede ser sobreescrita.");

                    if (mallaExistente.Turnos != null && mallaExistente.Turnos.Any())
                    {
                        _context.PlanificacionesTurno.RemoveRange(mallaExistente.Turnos);
                    }
                    _context.MallasCabecera.Remove(mallaExistente);
                    await _context.SaveChangesAsync();
                }

                var fechaInicioMes = new DateTime(anio, mes, 1);
                var fechaFinMes = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));

                var turnosHuerfanos = await _context.PlanificacionesTurno
                    .Where(p => p.FechaTurno >= fechaInicioMes && p.FechaTurno <= fechaFinMes)
                    .ToListAsync();

                if (turnosHuerfanos.Any())
                {
                    _context.PlanificacionesTurno.RemoveRange(turnosHuerfanos);
                    await _context.SaveChangesAsync();
                }

                // 2. Obtención de operadores activos
                var operadores = await _context.Colaboradores
                    .Where(c => c.IdRol == 2 && c.Estado == "Activo")
                    .ToListAsync();

                if (operadores.Count < esquema.DotacionMinima)
                    throw new InvalidOperationException($"Dotación insuficiente. Se requieren al menos {esquema.DotacionMinima} operadores activos (disponibles: {operadores.Count}).");

                var random = Random.Shared;
                operadores = operadores.OrderBy(_ => random.Next()).ToList();

                // 3. Mapeo de tipos de turno físicos
                var tiposTurno = await _context.TiposTurno.ToListAsync();

                var turno12D = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T4X4-D")
                               ?? tiposTurno.FirstOrDefault(t => !t.EsNocturno && t.DuracionHoras >= 11.5m);

                var turno12N = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T4X4-N")
                               ?? tiposTurno.FirstOrDefault(t => t.EsNocturno && t.DuracionHoras >= 11.5m);

                var turno8M = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T5X2-M")
                              ?? tiposTurno.FirstOrDefault(t => !t.EsNocturno && t.DuracionHoras <= 8.5m)
                              ?? tiposTurno.First();

                var turno8T = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T5X2-T")
                              ?? tiposTurno.FirstOrDefault(t => !t.EsNocturno && t.HoraInicio >= new TimeSpan(14, 0, 0))
                              ?? turno8M;

                var turno8N = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T5X2-N")
                              ?? tiposTurno.FirstOrDefault(t => t.EsNocturno && t.DuracionHoras <= 8.5m)
                              ?? turno8M;

                var turnoOficina = tiposTurno.FirstOrDefault(t => t.CodigoTurno == "T5X2-D") ?? turno8M;

                // 4. Identificación de modalidad operativa
                string tipoJornadaNorm = (esquema.TipoJornada ?? "").ToLower();
                string nombreEsquemaNorm = (esquema.NombreEsquema ?? "").ToLower();

                // Detección 1: ¿Es un ciclo de 12 horas? (4x4, 7x7, 10x10, etc.)
                bool es12Horas = (esquema.DiasTrabajoCiclo >= 3 && esquema.DiasDescansoCiclo >= 3)
                     || (turno12D != null && turno12D.DuracionHoras >= 11.5m && esquema.DiasDescansoCiclo > 2);

                // Detección 2: ¿Es un único turno diurno (sin noche)?
                bool esUnTurnoDiurno = tipoJornadaNorm.Contains("1 turno")
                                     || nombreEsquemaNorm.Contains("1 turno")
                                     || tipoJornadaNorm.Contains("diurno")
                                     || nombreEsquemaNorm.Contains("diurno")
                                     || esquema.DotacionMinima == 1;

                // Detección 3: Días de cobertura
                bool esLunesAViernes = tipoJornadaNorm.Contains("l-v")
                                     || tipoJornadaNorm.Contains("lunes a viernes")
                                     || nombreEsquemaNorm.Contains("l-v");

                bool esLunesASabado = !esLunesAViernes && (
                    tipoJornadaNorm.Contains("l-s")
                    || tipoJornadaNorm.Contains("sabado")
                    || tipoJornadaNorm.Contains("sábado")
                    || nombreEsquemaNorm.Contains("l-s")
                    || nombreEsquemaNorm.Contains("sabado")
                    || nombreEsquemaNorm.Contains("sábado")
                    || (esquema.DiasTrabajoCiclo == 6 && esquema.DiasDescansoCiclo == 1)
                );

                bool esSemanaCompleta = !esLunesAViernes && !esLunesASabado;

                // Detección 4: Solo es rotativo multiactivo si NO es 12h y NO es 1 solo turno diurno
                bool esRotativo8h = !es12Horas && !esUnTurnoDiurno && (
                    tipoJornadaNorm.Contains("3 turnos")
                    || tipoJornadaNorm.Contains("24/7")
                    || tipoJornadaNorm.Contains("24/5")
                    || tipoJornadaNorm.Contains("rotativo")
                );

                var nuevaMalla = new MallaCabecera
                {
                    IdEsquema = idEsquema,
                    IdSupervisorCrea = idSupervisorCrea,
                    PeriodoMes = mes,
                    PeriodoAnio = anio,
                    EstadoMalla = "Borrador",
                    FechaCreacion = DateTime.Now
                };

                _context.MallasCabecera.Add(nuevaMalla);
                await _context.SaveChangesAsync();

                int totalDiasMes = DateTime.DaysInMonth(anio, mes);
                var planificaciones = new List<PlanificacionTurno>();

                // Detección de domingos del mes para asignación equitativa anticipada (Art. 38 CT)
                var domingosDelMes = Enumerable.Range(1, totalDiasMes)
                    .Select(d => new DateTime(anio, mes, d))
                    .Where(f => f.DayOfWeek == DayOfWeek.Sunday)
                    .ToList();

                var grupoDomingoPorColaborador = new Dictionary<int, int>();
                for (int i = 0; i < operadores.Count; i++)
                {
                    grupoDomingoPorColaborador[operadores[i].IdColaborador] = i % 2;
                }

                // Control de carga y descansos
                var turnosAsignadosPorColaborador = operadores.ToDictionary(o => o.IdColaborador, _ => 0);
                var conteoNochesPorColab = operadores.ToDictionary(o => o.IdColaborador, _ => 0);
                var nochesSeguidasPorColaborador = operadores.ToDictionary(o => o.IdColaborador, _ => 0);
                var diasSeguidosPorColaborador = operadores.ToDictionary(o => o.IdColaborador, _ => 0);
                var finUltimoTurnoPorColaborador = new Dictionary<int, DateTime>();

                (DateTime Inicio, DateTime Fin) CalcularRangoTurno(DateTime fecha, TipoTurno t)
                {
                    DateTime inicio = fecha.Date.Add(t.HoraInicio);
                    DateTime fin = (t.HoraFin <= t.HoraInicio)
                        ? fecha.Date.AddDays(1).Add(t.HoraFin)
                        : fecha.Date.Add(t.HoraFin);
                    return (inicio, fin);
                }

                // -------------------------------------------------------------
                // PRECARGA HISTÓRICA: Últimos turnos del mes anterior
                // -------------------------------------------------------------
                var fechaUltimoDiaMesAnterior = fechaInicioMes.AddDays(-1);
                var turnosFinMesAnterior = await _context.PlanificacionesTurno
                    .Include(p => p.TipoTurno)
                    .Where(p => p.FechaTurno >= fechaInicioMes.AddDays(-7) && p.FechaTurno <= fechaUltimoDiaMesAnterior)
                    .OrderByDescending(p => p.FechaTurno)
                    .ToListAsync();

                var ultimoTurnoRegistrado = new Dictionary<int, PlanificacionTurno>();
                foreach (var pt in turnosFinMesAnterior)
                {
                    if (!ultimoTurnoRegistrado.ContainsKey(pt.IdColaborador))
                    {
                        ultimoTurnoRegistrado[pt.IdColaborador] = pt;
                        if (pt.TipoTurno != null)
                        {
                            var (_, fin) = CalcularRangoTurno(pt.FechaTurno, pt.TipoTurno);
                            finUltimoTurnoPorColaborador[pt.IdColaborador] = fin;

                            if (pt.TipoTurno.EsNocturno && pt.FechaTurno == fechaUltimoDiaMesAnterior)
                            {
                                nochesSeguidasPorColaborador[pt.IdColaborador] = 1;
                            }
                        }
                    }
                }

                // =========================================================================
                // MODALIDAD 1: ROTATIVO 8H L-V (24/5) CON DOTACIÓN DINÁMICA (N >= 3)
                // Bloque homogéneo semanal: 5 días mismo turno, Sáb-Dom libres (40h semanales)
                // =========================================================================
                if (esRotativo8h && esLunesAViernes && operadores.Count >= 3)
                {
                    int totalOperadores = operadores.Count;

                    var listaRoles = new List<TipoTurno> { turno8M, turno8T, turno8N };
                    int operadoresSobrantes = totalOperadores - 3;

                    while (operadoresSobrantes > 0)
                    {
                        listaRoles.Insert(0, turno8M);
                        operadoresSobrantes--;

                        if (operadoresSobrantes > 0)
                        {
                            listaRoles.Add(turno8T);
                            operadoresSobrantes--;
                        }
                    }

                    var rolesSemanales = listaRoles.ToArray();

                    var rolInicialPorColab = new Dictionary<int, int>();
                    var rolesOcupados = new HashSet<int>();

                    for (int i = 0; i < totalOperadores; i++)
                    {
                        var op = operadores[i];
                        int rolSugerido = -1;

                        if (ultimoTurnoRegistrado.TryGetValue(op.IdColaborador, out var ultPlanif) && ultPlanif.TipoTurno != null)
                        {
                            if (ultPlanif.TipoTurno.EsNocturno)
                            {
                                for (int r = 0; r < totalOperadores; r++)
                                {
                                    if (!rolesOcupados.Contains(r) && rolesSemanales[r].CodigoTurno == "T5X2-M")
                                    {
                                        rolSugerido = r;
                                        break;
                                    }
                                }
                            }
                            else if (ultPlanif.TipoTurno.CodigoTurno == "T5X2-M")
                            {
                                for (int r = 0; r < totalOperadores; r++)
                                {
                                    if (!rolesOcupados.Contains(r) && rolesSemanales[r].CodigoTurno == "T5X2-T")
                                    {
                                        rolSugerido = r;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int r = 0; r < totalOperadores; r++)
                                {
                                    if (!rolesOcupados.Contains(r) && rolesSemanales[r].EsNocturno)
                                    {
                                        rolSugerido = r;
                                        break;
                                    }
                                }
                            }
                        }

                        if (rolSugerido == -1 || rolesOcupados.Contains(rolSugerido))
                        {
                            for (int r = 0; r < totalOperadores; r++)
                            {
                                if (!rolesOcupados.Contains(r))
                                {
                                    rolSugerido = r;
                                    break;
                                }
                            }
                        }

                        rolesOcupados.Add(rolSugerido);
                        rolInicialPorColab[op.IdColaborador] = rolSugerido;
                    }

                    int semanaBase = ISOWeek.GetWeekOfYear(fechaInicioMes);

                    for (int dia = 1; dia <= totalDiasMes; dia++)
                    {
                        var fechaActual = new DateTime(anio, mes, dia);

                        if (fechaActual.DayOfWeek == DayOfWeek.Saturday || fechaActual.DayOfWeek == DayOfWeek.Sunday)
                            continue;

                        int semanaActual = ISOWeek.GetWeekOfYear(fechaActual);
                        int deltaSemana = semanaActual - semanaBase;
                        if (deltaSemana < 0) deltaSemana += 52;

                        foreach (var op in operadores)
                        {
                            int rolBase = rolInicialPorColab[op.IdColaborador];
                            int indiceTurnoSemanal = (rolBase + deltaSemana) % totalOperadores;
                            var turnoDia = rolesSemanales[indiceTurnoSemanal];

                            var (inicioTurno, finTurno) = CalcularRangoTurno(fechaActual, turnoDia);

                            if (finUltimoTurnoPorColaborador.TryGetValue(op.IdColaborador, out var finPrevio))
                            {
                                if ((inicioTurno - finPrevio).TotalHours < 8.0)
                                    continue;
                            }

                            var (esValido, _) = await _ruleValidator.ValidarDescansoInterjornadaAsync(
                                op.IdColaborador,
                                fechaActual,
                                turnoDia.IdTipoTurno
                            );

                            if (esValido)
                            {
                                planificaciones.Add(new PlanificacionTurno
                                {
                                    IdMalla = nuevaMalla.IdMalla,
                                    Malla = nuevaMalla,
                                    IdColaborador = op.IdColaborador,
                                    IdTipoTurno = turnoDia.IdTipoTurno,
                                    FechaTurno = fechaActual,
                                    EstadoTurno = "Programado"
                                });

                                finUltimoTurnoPorColaborador[op.IdColaborador] = finTurno;
                            }
                        }
                    }
                }
                // =========================================================================
                // MODALIDAD 2: ROTATIVO 8H GENERAL (Cobertura 24/7 de 7 días o 6+ operadores)
                // =========================================================================
                else if (esRotativo8h)
                {
                    for (int dia = 1; dia <= totalDiasMes; dia++)
                    {
                        var fechaActual = new DateTime(anio, mes, dia);

                        if (esLunesAViernes && (fechaActual.DayOfWeek == DayOfWeek.Saturday || fechaActual.DayOfWeek == DayOfWeek.Sunday))
                            continue;

                        if (esLunesASabado && fechaActual.DayOfWeek == DayOfWeek.Sunday)
                            continue;

                        int indiceDomingo = -1;
                        if (fechaActual.DayOfWeek == DayOfWeek.Sunday && !esLunesAViernes && !esLunesASabado)
                        {
                            indiceDomingo = domingosDelMes.IndexOf(fechaActual);
                        }

                        int cuposNoche = 1;
                        int cuposTarde = 1;
                        int cuposManana = 1;

                        if (indiceDomingo >= 0)
                        {
                            cuposNoche = 1; cuposTarde = 1; cuposManana = 1;
                        }
                        else if (operadores.Count >= 8)
                        {
                            cuposNoche = 2; cuposTarde = 2; cuposManana = 2;
                        }
                        else if (operadores.Count == 7)
                        {
                            cuposNoche = 1; cuposTarde = 2; cuposManana = 2;
                        }
                        else if (operadores.Count == 6)
                        {
                            cuposNoche = 1;
                            if (dia % 2 == 1) { cuposManana = 2; cuposTarde = 1; }
                            else { cuposManana = 1; cuposTarde = 2; }
                        }

                        var asignadosHoy = new HashSet<int>();
                        var asignacionesPorFranja = new (TipoTurno Turno, int Requeridos)[]
                        {
                            (turno8N, cuposNoche),
                            (turno8T, cuposTarde),
                            (turno8M, cuposManana)
                        };

                        foreach (var (franja, requeridos) in asignacionesPorFranja)
                        {
                            int cuposAsignados = 0;
                            var (inicioNuevoTurno, finNuevoTurno) = CalcularRangoTurno(fechaActual, franja);

                            var candidatos = operadores
                                .Where(o => !asignadosHoy.Contains(o.IdColaborador))
                                .Where(o => indiceDomingo == -1 || grupoDomingoPorColaborador[o.IdColaborador] != (indiceDomingo % 2))
                                .Where(o => diasSeguidosPorColaborador[o.IdColaborador] < 6)
                                .Where(o => !franja.EsNocturno || nochesSeguidasPorColaborador[o.IdColaborador] < 3)
                                .OrderBy(o => turnosAsignadosPorColaborador[o.IdColaborador])
                                .ThenBy(o => franja.EsNocturno ? conteoNochesPorColab[o.IdColaborador] : 0)
                                .ThenBy(o => diasSeguidosPorColaborador[o.IdColaborador])
                                .ThenBy(_ => random.Next())
                                .ToList();

                            foreach (var candidato in candidatos)
                            {
                                if (cuposAsignados >= requeridos) break;

                                if (finUltimoTurnoPorColaborador.TryGetValue(candidato.IdColaborador, out var finPrevio))
                                {
                                    double horasDescanso = (inicioNuevoTurno - finPrevio).TotalHours;
                                    if (horasDescanso < 8.0) continue;
                                }

                                var (esValido, _) = await _ruleValidator.ValidarDescansoInterjornadaAsync(
                                    candidato.IdColaborador,
                                    fechaActual,
                                    franja.IdTipoTurno
                                );

                                if (esValido)
                                {
                                    planificaciones.Add(new PlanificacionTurno
                                    {
                                        IdMalla = nuevaMalla.IdMalla,
                                        Malla = nuevaMalla,
                                        IdColaborador = candidato.IdColaborador,
                                        IdTipoTurno = franja.IdTipoTurno,
                                        FechaTurno = fechaActual,
                                        EstadoTurno = "Programado"
                                    });

                                    asignadosHoy.Add(candidato.IdColaborador);
                                    turnosAsignadosPorColaborador[candidato.IdColaborador]++;
                                    diasSeguidosPorColaborador[candidato.IdColaborador]++;
                                    finUltimoTurnoPorColaborador[candidato.IdColaborador] = finNuevoTurno;

                                    if (franja.EsNocturno)
                                    {
                                        conteoNochesPorColab[candidato.IdColaborador]++;
                                        nochesSeguidasPorColaborador[candidato.IdColaborador]++;
                                    }
                                    else
                                    {
                                        nochesSeguidasPorColaborador[candidato.IdColaborador] = 0;
                                    }

                                    cuposAsignados++;
                                }
                            }
                        }

                        foreach (var op in operadores)
                        {
                            if (!asignadosHoy.Contains(op.IdColaborador))
                            {
                                diasSeguidosPorColaborador[op.IdColaborador] = 0;
                                nochesSeguidasPorColaborador[op.IdColaborador] = 0;
                            }
                        }
                    }
                }
                // =========================================================================
                // MODALIDAD 3: TURNOS DE 12 HORAS (4x4, 7x7, 10x10) PARAMÉTRICO
                // =========================================================================
                else if (es12Horas)
                {
                    int diasT = esquema.DiasTrabajoCiclo > 0 ? esquema.DiasTrabajoCiclo : 4;
                    int diasD = esquema.DiasDescansoCiclo > 0 ? esquema.DiasDescansoCiclo : 4;
                    int duracionCiclo = diasT + diasD;
                    int totalOperadores = operadores.Count;
                    var turno12Efectivo = turno12D ?? turnoOficina;

                    bool es12hSoloDia = esUnTurnoDiurno
                                       || tipoJornadaNorm.Contains("1 turno")
                                       || nombreEsquemaNorm.Contains("1 turno");

                    if (es12hSoloDia)
                    {
                        // CASO A: 12H EXCLUSIVAMENTE DIURNO (Sin noche, relevo Grupo A y Grupo B)
                        for (int dia = 1; dia <= totalDiasMes; dia++)
                        {
                            var fechaActual = new DateTime(anio, mes, dia);

                            for (int i = 0; i < totalOperadores; i++)
                            {
                                var op = operadores[i];
                                int grupo = i % 2;
                                int offset = grupo == 0 ? 0 : diasT;
                                int diaEnCiclo = ((dia - 1) + offset) % duracionCiclo;

                                if (diaEnCiclo < diasT)
                                {
                                    planificaciones.Add(new PlanificacionTurno
                                    {
                                        IdMalla = nuevaMalla.IdMalla,
                                        Malla = nuevaMalla,
                                        IdColaborador = op.IdColaborador,
                                        IdTipoTurno = turno12Efectivo.IdTipoTurno,
                                        FechaTurno = fechaActual,
                                        EstadoTurno = "Programado"
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        // CASO B: 12H CONTINUO 24/7 (Matriz de 4 fases Día y Noche)
                        int superCiclo = duracionCiclo * 2;
                        int[] offsetsBase = { 0, 0, diasT, diasT };
                        bool[] iniciaConDiaBase = { true, false, true, false };

                        var ordenDistribucionSobrantes = new int[] { 0, 2, 1, 3 };
                        var fasesAsignadas = new List<int> { 0, 1, 2, 3 };

                        for (int i = 4; i < totalOperadores; i++)
                        {
                            int faseRefuerzo = ordenDistribucionSobrantes[(i - 4) % 4];
                            fasesAsignadas.Add(faseRefuerzo);
                        }

                        var fasePorColaborador = new Dictionary<int, int>();
                        var fasesOcupadas = new List<int>(fasesAsignadas);

                        for (int i = 0; i < totalOperadores; i++)
                        {
                            var op = operadores[i];
                            int faseSeleccionada = -1;

                            if (ultimoTurnoRegistrado.TryGetValue(op.IdColaborador, out var ultPlanif) && ultPlanif.TipoTurno != null)
                            {
                                if (ultPlanif.TipoTurno.EsNocturno)
                                {
                                    faseSeleccionada = fasesOcupadas.FirstOrDefault(f => f == 2 || f == 3 || f == 1, -1);
                                }
                                else
                                {
                                    faseSeleccionada = fasesOcupadas.FirstOrDefault(f => f == 0 || f == 2, -1);
                                }
                            }

                            if (faseSeleccionada == -1 || !fasesOcupadas.Contains(faseSeleccionada))
                            {
                                faseSeleccionada = fasesOcupadas.First();
                            }

                            fasesOcupadas.Remove(faseSeleccionada);
                            fasePorColaborador[op.IdColaborador] = faseSeleccionada;
                        }

                        for (int dia = 1; dia <= totalDiasMes; dia++)
                        {
                            var fechaActual = new DateTime(anio, mes, dia);

                            foreach (var operador in operadores)
                            {
                                int fase = fasePorColaborador[operador.IdColaborador];
                                int offset = offsetsBase[fase];
                                bool empiezaConDia = iniciaConDiaBase[fase];

                                int diaRelativo = ((dia - 1) + offset) % superCiclo;
                                bool esPrimerCiclo = diaRelativo < duracionCiclo;
                                int diaEnBloque = diaRelativo % duracionCiclo;

                                TipoTurno? turnoAsignado = null;

                                if (diaEnBloque < diasT)
                                {
                                    bool tocaDia = esPrimerCiclo ? empiezaConDia : !empiezaConDia;
                                    turnoAsignado = tocaDia ? turno12Efectivo : (turno12N ?? turno12Efectivo);
                                }

                                if (turnoAsignado != null)
                                {
                                    var (inicioTurno, finTurno) = CalcularRangoTurno(fechaActual, turnoAsignado);

                                    if (finUltimoTurnoPorColaborador.TryGetValue(operador.IdColaborador, out var finPrevio))
                                    {
                                        if ((inicioTurno - finPrevio).TotalHours < 12.0)
                                            continue;
                                    }

                                    var (esValido, _) = await _ruleValidator.ValidarDescansoInterjornadaAsync(
                                        operador.IdColaborador,
                                        fechaActual,
                                        turnoAsignado.IdTipoTurno
                                    );

                                    if (esValido)
                                    {
                                        planificaciones.Add(new PlanificacionTurno
                                        {
                                            IdMalla = nuevaMalla.IdMalla,
                                            Malla = nuevaMalla,
                                            IdColaborador = operador.IdColaborador,
                                            IdTipoTurno = turnoAsignado.IdTipoTurno,
                                            FechaTurno = fechaActual,
                                            EstadoTurno = "Programado"
                                        });

                                        finUltimoTurnoPorColaborador[operador.IdColaborador] = finTurno;
                                    }
                                }
                            }
                        }
                    }
                }
                // =========================================================================
                // MODALIDAD 4: UN SOLO TURNO DIURNO (ADMINISTRATIVO L-V, L-S O 5x2 DE 7 DÍAS)
                // =========================================================================
                else
                {
                    if (esSemanaCompleta && operadores.Count >= 2)
                    {
                        // 5x2 continuo de 7 días con 1 turno diurno (Lunes a Domingo)
                        int semanaBase = ISOWeek.GetWeekOfYear(fechaInicioMes);

                        for (int dia = 1; dia <= totalDiasMes; dia++)
                        {
                            var fechaActual = new DateTime(anio, mes, dia);
                            int semanaActual = ISOWeek.GetWeekOfYear(fechaActual);
                            int deltaSemana = semanaActual - semanaBase;
                            if (deltaSemana < 0) deltaSemana += 52;

                            for (int i = 0; i < operadores.Count; i++)
                            {
                                var op = operadores[i];
                                int grupo = (i + deltaSemana) % 2;

                                bool esDiaLibre = false;
                                if (grupo == 0)
                                {
                                    esDiaLibre = (fechaActual.DayOfWeek == DayOfWeek.Saturday || fechaActual.DayOfWeek == DayOfWeek.Sunday);
                                }
                                else
                                {
                                    esDiaLibre = (fechaActual.DayOfWeek == DayOfWeek.Monday || fechaActual.DayOfWeek == DayOfWeek.Tuesday);
                                }

                                if (!esDiaLibre)
                                {
                                    planificaciones.Add(new PlanificacionTurno
                                    {
                                        IdMalla = nuevaMalla.IdMalla,
                                        Malla = nuevaMalla,
                                        IdColaborador = op.IdColaborador,
                                        IdTipoTurno = turnoOficina.IdTipoTurno,
                                        FechaTurno = fechaActual,
                                        EstadoTurno = "Programado"
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        // Diurno estándar Lunes a Viernes o Lunes a Sábado
                        for (int dia = 1; dia <= totalDiasMes; dia++)
                        {
                            var fechaActual = new DateTime(anio, mes, dia);

                            // El domingo siempre es día libre legal (Art. 35 CT)
                            if (fechaActual.DayOfWeek == DayOfWeek.Sunday)
                                continue;

                            // El sábado solo es libre si el esquema es exclusivamente Lunes a Viernes
                            if (!esLunesASabado && fechaActual.DayOfWeek == DayOfWeek.Saturday)
                                continue;

                            foreach (var operador in operadores)
                            {
                                planificaciones.Add(new PlanificacionTurno
                                {
                                    IdMalla = nuevaMalla.IdMalla,
                                    Malla = nuevaMalla,
                                    IdColaborador = operador.IdColaborador,
                                    IdTipoTurno = turnoOficina.IdTipoTurno,
                                    FechaTurno = fechaActual,
                                    EstadoTurno = "Programado"
                                });
                            }
                        }
                    }
                }

                await _context.PlanificacionesTurno.AddRangeAsync(planificaciones);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return nuevaMalla;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}