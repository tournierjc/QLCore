﻿/*
 Copyright (C) 2020 Jean-Camille Tournier (mail@tournierjc.fr)

 This file is part of QLCore Project https://github.com/OpenDerivatives/QLCore

 QLCore is free software: you can redistribute it and/or modify it
 under the terms of the QLCore and QLNet license. You should have received a
 copy of the license along with this program; if not, license is
 available at https://github.com/OpenDerivatives/QLCore/LICENSE.

 QLCore is a forked of QLNet which is a based on QuantLib, a free-software/open-source
 library for financial quantitative analysts and developers - http://quantlib.org/
 The QuantLib license is available online at http://quantlib.org/license.shtml and the
 QLNet license is available online at https://github.com/amaggiulli/QLNet/blob/develop/LICENSE.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICAR PURPOSE. See the license for more details.
*/

using System;
using System.Collections.Generic;

namespace QLCore
{
   /// <summary>
   /// Finite-Differences Black Scholes barrier option engine
   /// </summary>
   public class FdBlackScholesBarrierEngine : DividendBarrierOption.Engine
   {
      // Constructor
      public FdBlackScholesBarrierEngine(
         GeneralizedBlackScholesProcess process,
         int tGrid = 100, int xGrid = 100, int dampingSteps = 0,
         FdmSchemeDesc schemeDesc = null,
         bool localVol = false,
         double? illegalLocalVolOverwrite = null)
      {
         process_ = process;
         tGrid_ = tGrid;
         xGrid_ = xGrid;
         dampingSteps_ = dampingSteps;
         schemeDesc_ = schemeDesc == null ? new FdmSchemeDesc().Douglas() : schemeDesc;
         localVol_ = localVol;
         illegalLocalVolOverwrite_ = illegalLocalVolOverwrite;
      }

      public override void calculate()
      {
         // 1. Mesher
         StrikedTypePayoff payoff = arguments_.payoff as StrikedTypePayoff;
         double maturity = process_.time(arguments_.exercise.lastDate());

         double? xMin = null;
         double? xMax = null;
         if (arguments_.barrierType == Barrier.Type.DownIn
             || arguments_.barrierType == Barrier.Type.DownOut)
         {
            xMin = Math.Log(arguments_.barrier.Value);
         }
         if (arguments_.barrierType == Barrier.Type.UpIn
             || arguments_.barrierType == Barrier.Type.UpOut)
         {
            xMax = Math.Log(arguments_.barrier.Value);
         }

         Fdm1dMesher equityMesher =
            new FdmBlackScholesMesher(xGrid_, process_, maturity,
                                      payoff.strike(), xMin, xMax, 0.0001, 1.5,
                                      new Pair < double?, double? >(),
                                      arguments_.cashFlow);

         FdmMesher mesher =
            new FdmMesherComposite(equityMesher);

         // 2. Calculator
         FdmInnerValueCalculator calculator =
            new FdmLogInnerValue(payoff, mesher, 0);

         // 3. Step conditions
         List<IStepCondition<Vector>> stepConditions = new List<IStepCondition<Vector>>();
         List<List<double>> stoppingTimes = new List<List<double>>();

         // 3.1 Step condition if discrete dividends
         FdmDividendHandler dividendCondition =
            new FdmDividendHandler(arguments_.cashFlow, mesher,
                                   process_.riskFreeRate().currentLink().referenceDate(),
                                   process_.riskFreeRate().currentLink().dayCounter(), 0);

         if (!arguments_.cashFlow.empty())
         {
            stepConditions.Add(dividendCondition);
            stoppingTimes.Add(dividendCondition.dividendTimes());
         }

         Utils.QL_REQUIRE(arguments_.exercise.type() == Exercise.Type.European,
                          () => "only european style option are supported");

         FdmStepConditionComposite conditions =
            new FdmStepConditionComposite(stoppingTimes, stepConditions);

         // 4. Boundary conditions
         FdmBoundaryConditionSet boundaries = new FdmBoundaryConditionSet();
         if (arguments_.barrierType == Barrier.Type.DownIn
             || arguments_.barrierType == Barrier.Type.DownOut)
         {
            boundaries.Add(
               new FdmDirichletBoundary(mesher, arguments_.rebate.Value, 0,
                                        FdmDirichletBoundary.Side.Lower));
         }

         if (arguments_.barrierType == Barrier.Type.UpIn
             || arguments_.barrierType == Barrier.Type.UpOut)
         {
            boundaries.Add(
               new FdmDirichletBoundary(mesher, arguments_.rebate.Value, 0,
                                        FdmDirichletBoundary.Side.Upper));
         }

         // 5. Solver
         FdmSolverDesc solverDesc = new FdmSolverDesc();
         solverDesc.mesher = mesher;
         solverDesc.bcSet = boundaries;
         solverDesc.condition = conditions;
         solverDesc.calculator = calculator;
         solverDesc.maturity = maturity;
         solverDesc.dampingSteps = dampingSteps_;
         solverDesc.timeSteps = tGrid_;

         FdmBlackScholesSolver solver =
            new FdmBlackScholesSolver(
            new Handle<GeneralizedBlackScholesProcess>(process_),
            payoff.strike(), solverDesc, schemeDesc_,
            localVol_, illegalLocalVolOverwrite_);

         double spot = process_.x0();
         results_.value = solver.valueAt(spot);
         results_.delta = solver.deltaAt(spot);
         results_.gamma = solver.gammaAt(spot);
         results_.theta = solver.thetaAt(spot);

         // 6. Calculate vanilla option and rebate for in-barriers
         if (arguments_.barrierType == Barrier.Type.DownIn
             || arguments_.barrierType == Barrier.Type.UpIn)
         {
            // Cast the payoff
            StrikedTypePayoff castedPayoff = arguments_.payoff as StrikedTypePayoff;

            // Calculate the vanilla option
            DividendVanillaOption vanillaOption =
               new DividendVanillaOption(castedPayoff, arguments_.exercise,
                                         dividendCondition.dividendDates(),
                                         dividendCondition.dividends());

            vanillaOption.setPricingEngine(
               new FdBlackScholesVanillaEngine(
                  process_, tGrid_, xGrid_,
                  0, // dampingSteps
                  schemeDesc_, localVol_, illegalLocalVolOverwrite_));

            // Calculate the rebate value
            DividendBarrierOption rebateOption =
               new DividendBarrierOption(arguments_.barrierType,
                                         arguments_.barrier.Value,
                                         arguments_.rebate.Value,
                                         castedPayoff, arguments_.exercise,
                                         dividendCondition.dividendDates(),
                                         dividendCondition.dividends());

            int min_grid_size = 50;
            int rebateDampingSteps
               = (dampingSteps_ > 0) ? Math.Min(1, dampingSteps_ / 2) : 0;

            rebateOption.setPricingEngine(new FdBlackScholesRebateEngine(
                                             process_, tGrid_, Math.Max(min_grid_size, xGrid_ / 5),
                                             rebateDampingSteps, schemeDesc_, localVol_,
                                             illegalLocalVolOverwrite_));

            results_.value = vanillaOption.NPV() + rebateOption.NPV()
                             - results_.value;
            results_.delta = vanillaOption.delta() + rebateOption.delta()
                             - results_.delta;
            results_.gamma = vanillaOption.gamma() + rebateOption.gamma()
                             - results_.gamma;
            results_.theta = vanillaOption.theta() + rebateOption.theta()
                             - results_.theta;
         }
      }

      protected GeneralizedBlackScholesProcess process_;
      protected int tGrid_, xGrid_, dampingSteps_;
      protected FdmSchemeDesc schemeDesc_;
      protected bool localVol_;
      protected double? illegalLocalVolOverwrite_;
   }
}
